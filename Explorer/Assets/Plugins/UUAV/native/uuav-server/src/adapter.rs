//! The IPC -> core dispatch loop. Owns the core's lifetime inside the
//! helper: `Configure` runs `uuav_init` against the helper's own device,
//! `Shutdown` (or a dead channel) tears everything down.

use crate::{device, state};
use anyhow::Context as _;
use crossbeam_channel::{Receiver, Sender, unbounded};
use std::collections::HashMap;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::sync::OnceLock;
use std::time::{Duration, Instant};
use uuav_core as core;
use uuav_ipc::channel::Channel;
use uuav_ipc::protocol::{LogSink, PlayerId, ReplyBody, ToClient, ToServer};

/// State-pump cadence; the client extrapolates media time between pushes.
const PUMP_INTERVAL: Duration = Duration::from_millis(20);

/// Video tick cadence (~60 Hz): render event + slot copy + publish.
const VIDEO_INTERVAL: Duration = Duration::from_millis(16);

/// Audio pull cadence; each pull covers the wall-clock elapsed since the
/// previous one so serve-loop stalls (video blits) don't starve the stream.
const AUDIO_INTERVAL: Duration = Duration::from_millis(10);

/// Upper bound on one pull's catch-up so a long stall can't burst-allocate.
const AUDIO_MAX_PULL: Duration = Duration::from_millis(100);

/// Client-ring cushion the feedback-paced pulls hold: between the ring's
/// 40 ms prime and its 150 ms high watermark, so neither wall is hit in
/// steady state.
const AUDIO_TARGET: Duration = Duration::from_millis(80);

/// Feedback older than this means the client stopped consuming (engine
/// audio paused/stalled): stop slaving the clock so it free-runs, matching
/// the pre-feedback behavior of video continuing without audio.
const FEEDBACK_FRESH: Duration = Duration::from_millis(250);

/// A consumption-derived clock this far from the core's own is nonsense
/// (stale ledger around a seek); skip the assignment and let the next
/// fresh pull re-derive it.
const MASTER_CLOCK_SANITY: f64 = 1.0;

/// Serve-loop health report cadence.
const SERVE_STATS_INTERVAL: Duration = Duration::from_secs(1);

/// Channel poll granularity for the single-threaded serve loop.
const POLL_TIMEOUT_MS: u32 = 5;

/// The core's callbacks fire from arbitrary FFmpeg/player threads; they
/// funnel into this in-proc channel and the serve loop forwards them on
/// the IPC channel.
static FORWARD: OnceLock<Sender<(LogSink, String)>> = OnceLock::new();

extern "C" fn error_sink(line: *const c_char) {
    forward(LogSink::Error, line);
}

extern "C" fn warning_sink(line: *const c_char) {
    forward(LogSink::Warning, line);
}

extern "C" fn log_sink(line: *const c_char) {
    forward(LogSink::Log, line);
}

fn forward(sink: LogSink, line: *const c_char) {
    if line.is_null() {
        return;
    }
    let Some(tx) = FORWARD.get() else {
        return;
    };
    let line = unsafe { CStr::from_ptr(line) }.to_string_lossy().into_owned();
    _ = tx.send((sink, line));
}

#[derive(Default)]
struct PlayerTracking {
    media_info_sent: bool,
    /// Frames sent to the client since this helper created the player.
    sent_frames: u64,
    /// Latest consumption report from the client's jitter ring.
    removed_frames: u64,
    /// When the latest report arrived; `None` until the first one, which
    /// switches the pull sizing from wall clock to the credit scheme.
    feedback_at: Option<Instant>,
}

impl PlayerTracking {
    fn feedback_fresh(&self) -> bool {
        self.feedback_at
            .is_some_and(|at| at.elapsed() < FEEDBACK_FRESH)
    }
}

/// Everything the serve loop owns across iterations.
struct Serve {
    /// Kept alive for the whole process: the core borrows the device it
    /// derived from the probe texture.
    probe: Option<device::ProbeDevice>,
    players: HashMap<PlayerId, PlayerTracking>,
    /// The negotiated output format the audio pump sizes its pulls by.
    audio: Option<uuav_ipc::protocol::AudioOptionsWire>,
    /// Reused pull buffer (sized for [`AUDIO_MAX_PULL`]).
    audio_scratch: Vec<f32>,
    video: Option<crate::video::VideoPump>,
}

pub fn run(
    channel: &mut Channel,
    #[cfg(target_os = "macos")] service: &str,
) -> anyhow::Result<()> {
    let (tx, rx) = unbounded();
    _ = FORWARD.set(tx);

    let mut serve = Serve {
        probe: None,
        players: HashMap::new(),
        audio: None,
        audio_scratch: Vec::new(),
        video: None,
    };
    let mut last_pump = Instant::now();
    let mut last_audio = Instant::now();
    let mut last_video = Instant::now();
    let mut last_stats = Instant::now();
    // serve-loop health for ToClient::ServeStats
    let mut iter_max = Duration::ZERO;
    let mut pull_clamps: u64 = 0;

    loop {
        channel.poll_readable(POLL_TIMEOUT_MS)?;
        // work time only: the idle poll above is not a stall
        let iter_start = Instant::now();

        while let Some(message) = channel.try_recv::<ToServer>()? {
            if dispatch(
                channel,
                message,
                &mut serve,
                #[cfg(target_os = "macos")]
                service,
            )? {
                core::uuav_deinit();
                return Ok(());
            }
        }

        forward_logs(channel, &rx)?;

        if last_pump.elapsed() >= PUMP_INTERVAL {
            last_pump = Instant::now();
            pump_states(channel, &mut serve.players)?;
        }

        if last_audio.elapsed() >= AUDIO_INTERVAL {
            // the clamp only bounds the feedback-less wall-clock sizing;
            // feedback-paced players carry their deficit in the credit
            // ledger, so a stall's backlog drains over the next ticks
            if last_audio.elapsed() > AUDIO_MAX_PULL {
                pull_clamps += 1;
            }
            let elapsed = last_audio.elapsed().min(AUDIO_MAX_PULL);
            last_audio = Instant::now();
            pump_audio(channel, &mut serve, elapsed)?;
        }

        if last_video.elapsed() >= VIDEO_INTERVAL {
            last_video = Instant::now();
            if let Some(video) = serve.video.as_mut() {
                video.tick(serve.players.keys().copied(), channel);
            }
        }

        iter_max = iter_max.max(iter_start.elapsed());
        if last_stats.elapsed() >= SERVE_STATS_INTERVAL {
            last_stats = Instant::now();
            let max_iter_us = u64::try_from(iter_max.as_micros()).unwrap_or(u64::MAX);
            iter_max = Duration::ZERO;
            channel
                .send(&ToClient::ServeStats {
                    max_iter_us,
                    audio_pull_clamps: pull_clamps,
                })
                .context("send ServeStats")?;
        }
    }
}

/// Returns `true` on `Shutdown`.
fn dispatch(
    channel: &mut Channel,
    message: ToServer,
    serve: &mut Serve,
    #[cfg(target_os = "macos")] service: &str,
) -> anyhow::Result<bool> {
    let players = &mut serve.players;
    match message {
        ToServer::Configure {
            corr,
            audio,
            protocol_whitelist,
            log_level,
            adapter,
        } => {
            let result = configure(&mut serve.probe, audio, &protocol_whitelist, log_level, adapter);
            if result.is_ok() {
                serve.audio = Some(audio);
            }
            let result = result.and_then(|()| {
                let probe = serve.probe.as_ref().ok_or("probe device is missing")?;
                #[cfg(target_os = "macos")]
                let pump = crate::video::VideoPump::new(probe, service);
                #[cfg(target_os = "windows")]
                let pump = crate::video::VideoPump::new(probe);
                serve.video = Some(pump.map_err(|e| e.to_string())?);
                Ok(())
            });
            reply(channel, corr, result.map(|()| ReplyBody::Unit))?;
        }
        ToServer::SetLogLevel { level } => core::uuav_set_log_level(level),
        ToServer::UpdateAudioOut { corr, audio } => {
            let result = update_audio_out(serve, audio);
            reply(channel, corr, result.map(|()| ReplyBody::Unit))?;
        }
        ToServer::PlayerNew { corr } => {
            let result = player_new(players);
            reply(channel, corr, result.map(ReplyBody::PlayerId))?;
        }
        ToServer::PlayerFree { id } => {
            core::uuav_player_free(id);
            players.remove(&id);
            if let Some(video) = serve.video.as_mut() {
                video.remove_player(id);
            }
        }
        ToServer::OpenMedia { id, url } => {
            let outcome = open_media(id, &url);
            if let Some(tracking) = players.get_mut(&id) {
                // new media, new info: re-announce once the core has it
                tracking.media_info_sent = false;
            }
            report_command_error(channel, id, "open media", outcome)?;
        }
        ToServer::CloseMedia { id } => {
            let outcome = state::consume_result(core::uuav_player_close_media(id));
            report_command_error(channel, id, "close media", outcome)?;
        }
        ToServer::Play { id } => {
            let outcome = state::consume_result(core::uuav_player_play(id));
            report_command_error(channel, id, "play", outcome)?;
        }
        ToServer::Pause { id } => {
            let outcome = state::consume_result(core::uuav_player_pause(id));
            report_command_error(channel, id, "pause", outcome)?;
        }
        ToServer::Seek { id, time } => {
            let outcome = state::consume_result(core::uuav_player_seek_async(id, time));
            report_command_error(channel, id, "seek", outcome)?;
        }
        ToServer::SetLooping { id, looping } => {
            let outcome =
                state::consume_result(core::uuav_player_set_looping(id, u8::from(looping)));
            report_command_error(channel, id, "set looping", outcome)?;
        }
        ToServer::SetRate { id, rate } => {
            let outcome = state::consume_result(core::uuav_player_set_rate(id, rate));
            report_command_error(channel, id, "set rate", outcome)?;
        }
        ToServer::AssignMasterClock { id, time } => {
            let outcome =
                state::consume_result(core::uuav_player_assign_master_clock(id, time));
            report_command_error(channel, id, "assign master clock", outcome)?;
        }
        ToServer::AudioFeedback { id, removed_frames } => {
            // unknown ids are stale (player freed mid-flight): ignore
            if let Some(tracking) = players.get_mut(&id) {
                tracking.removed_frames = removed_frames;
                tracking.feedback_at = Some(Instant::now());
            }
        }
        ToServer::TextureSetAck { id, generation } => {
            if let Some(video) = serve.video.as_mut() {
                video.ack(id, generation);
            }
        }
        ToServer::Shutdown => return Ok(true),
    }
    Ok(false)
}

fn configure(
    probe: &mut Option<device::ProbeDevice>,
    audio: uuav_ipc::protocol::AudioOptionsWire,
    protocol_whitelist: &str,
    log_level: i32,
    adapter: u64,
) -> Result<(), String> {
    let created = device::ProbeDevice::new(adapter).map_err(|e| e.to_string())?;
    let whitelist = CString::new(protocol_whitelist).map_err(|e| e.to_string())?;

    let result = unsafe {
        core::uuav_init(
            created.probe_ptr(),
            core::AudioOptionsRaw {
                sample_rate: audio.sample_rate,
                channels: audio.channels,
            },
            Some(error_sink),
            Some(warning_sink),
            Some(log_sink),
            whitelist.as_ptr(),
            log_level,
        )
    };
    state::consume_result(result)?;

    *probe = Some(created);
    Ok(())
}

fn update_audio_out(
    serve: &mut Serve,
    audio: uuav_ipc::protocol::AudioOptionsWire,
) -> Result<(), String> {
    state::consume_result(core::uuav_update_audio_out(core::AudioOptionsRaw {
        sample_rate: audio.sample_rate,
        channels: audio.channels,
    }))?;
    serve.audio = Some(audio);
    // the client drops its jitter rings (and their consumption ledgers) on
    // the reply; restart ours in lockstep or the credit math would starve
    // every pull. A stale in-flight feedback right after this self-heals:
    // the next report carries the reset ledger.
    for tracking in serve.players.values_mut() {
        tracking.sent_frames = 0;
        tracking.removed_frames = 0;
        tracking.feedback_at = None;
    }
    Ok(())
}

fn player_new(players: &mut HashMap<PlayerId, PlayerTracking>) -> Result<PlayerId, String> {
    let result = core::uuav_player_new();
    if !result.error_message.is_null() {
        // reuse the ResultFFI consume path for the owned error string
        return state::consume_result(core::ResultFFI {
            error_message: result.error_message,
        })
        .map(|()| 0);
    }
    players.insert(result.player_id, PlayerTracking::default());
    Ok(result.player_id)
}

fn open_media(id: PlayerId, url: &str) -> Result<(), String> {
    let url = CString::new(url).map_err(|e| e.to_string())?;
    state::consume_result(unsafe { core::uuav_player_open_media_async(id, url.as_ptr()) })
}

/// Fire-and-forget commands report failures through the player error event
/// (the client routes it into the same C# error callback as today).
fn report_command_error(
    channel: &mut Channel,
    id: PlayerId,
    command: &str,
    outcome: Result<(), String>,
) -> anyhow::Result<()> {
    if let Err(message) = outcome {
        channel
            .send(&ToClient::PlayerError {
                id: Some(id),
                message: format!("uuav {command} failed: {message}"),
            })
            .context("send PlayerError")?;
    }
    Ok(())
}

fn reply(
    channel: &mut Channel,
    corr: u32,
    result: Result<ReplyBody, String>,
) -> anyhow::Result<()> {
    channel
        .send(&ToClient::Reply { corr, result })
        .context("send Reply")
}

fn forward_logs(channel: &mut Channel, rx: &Receiver<(LogSink, String)>) -> anyhow::Result<()> {
    while let Ok((sink, line)) = rx.try_recv() {
        channel
            .send(&ToClient::Log { sink, line })
            .context("send Log")?;
    }
    Ok(())
}

/// Pulls audio from every player and forwards non-empty reads.
///
/// Players whose client reported consumption ([`ToServer::AudioFeedback`])
/// are pulled by the credit scheme: enough frames to keep
/// `sent - removed` at [`AUDIO_TARGET`], capped at [`AUDIO_MAX_PULL`] per
/// tick. The client's actual consumption rate paces production, so the
/// jitter ring can neither drift into its high watermark nor run dry from
/// clock skew, and a stall's deficit persists in the ledger instead of
/// being clamped away. Feedback-less players (headless consumers) keep
/// the elapsed-wall-clock sizing.
///
/// Fresh feedback also slaves the core clock to the speaker position
/// (`head_pts` minus the frames still in flight), so content release and
/// consumption cannot drift apart — the core's ±150 ms drift correction
/// becomes a dead band in steady state.
fn pump_audio(
    channel: &mut Channel,
    serve: &mut Serve,
    elapsed: Duration,
) -> anyhow::Result<()> {
    let Serve {
        players,
        audio,
        audio_scratch,
        ..
    } = serve;
    let Some(audio) = *audio else {
        return Ok(());
    };
    let sample_rate = f64::from(audio.sample_rate);
    let channels = audio.channels.max(1) as usize;
    let wall_frames = (sample_rate * elapsed.as_secs_f64()) as i64;
    let max_pull_frames = (sample_rate * AUDIO_MAX_PULL.as_secs_f64()) as u64;
    let target_frames = (sample_rate * AUDIO_TARGET.as_secs_f64()) as u64;

    for (&id, tracking) in players.iter_mut() {
        let frames = if tracking.feedback_at.is_some() {
            tracking
                .removed_frames
                .saturating_add(target_frames)
                .saturating_sub(tracking.sent_frames)
                .min(max_pull_frames)
        } else {
            wall_frames.max(0) as u64
        };
        let Ok(frames) = i32::try_from(frames) else {
            continue;
        };
        if frames <= 0 {
            continue;
        }
        let samples = frames as usize * channels;
        audio_scratch.resize(samples, 0.0);

        let mut head_pts = f64::NAN;
        let read = unsafe {
            core::uuav_player_read_audio_pts(id, audio_scratch.as_mut_ptr(), frames, &mut head_pts)
        };
        if read <= 0 {
            continue;
        }

        // consumption-slaved master clock: the sample the speaker plays now
        // sits `sent - removed` frames behind this pull's head
        if tracking.feedback_fresh() && head_pts.is_finite() {
            let outstanding = tracking.sent_frames.saturating_sub(tracking.removed_frames);
            let rate = core::uuav_player_get_rate(id);
            // bounded by the in-flight window (target + max pull), far
            // below f64's 2^52 mantissa
            #[allow(clippy::cast_precision_loss)]
            let speaker_time = (outstanding as f64).mul_add(-rate / sample_rate, head_pts);
            let mut core_time = 0.0_f64;
            let in_sanity = state::consume_result(unsafe {
                core::uuav_player_current_time(id, &mut core_time)
            })
            .is_ok()
                && (speaker_time - core_time).abs() <= MASTER_CLOCK_SANITY;
            if in_sanity {
                // sub-threshold corrections are ignored by the core (snap
                // hysteresis); a failure is already surfaced elsewhere
                let _ = state::consume_result(core::uuav_player_assign_master_clock(
                    id,
                    speaker_time,
                ));
            }
        }

        tracking.sent_frames = tracking.sent_frames.saturating_add(read as u64);
        let filled = read as usize * channels;
        let Some(payload) = audio_scratch.get(..filled) else {
            continue;
        };
        channel.send(&ToClient::AudioPacket {
            id,
            samples: payload.to_vec(),
        })?;
    }
    Ok(())
}

fn pump_states(
    channel: &mut Channel,
    players: &mut HashMap<PlayerId, PlayerTracking>,
) -> anyhow::Result<()> {
    for (&id, tracking) in players.iter_mut() {
        channel
            .send(&ToClient::State(state::snapshot(id)))
            .context("send State")?;

        if !tracking.media_info_sent
            && let Some(info) = state::media_info(id)
        {
            tracking.media_info_sent = true;
            channel
                .send(&ToClient::MediaInfo { id, info })
                .context("send MediaInfo")?;
        }
    }
    Ok(())
}
