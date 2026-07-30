//! The IPC -> core dispatch loop. Owns the core's lifetime inside the
//! helper: `Configure` runs `uuav_init` against the helper's own device,
//! `Shutdown` (or a dead socket) tears everything down.

use crate::{device, state};
use anyhow::Context as _;
use crossbeam_channel::{Receiver, Sender, unbounded};
use std::collections::HashMap;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::sync::OnceLock;
use std::time::{Duration, Instant};
use uuav_core as core;
use uuav_ipc::protocol::{LogSink, PlayerId, ReplyBody, ToClient, ToServer};
use uuav_ipc::{socket, zmq};

/// State-pump cadence; the client extrapolates media time between pushes.
const PUMP_INTERVAL: Duration = Duration::from_millis(20);

/// Video tick cadence (~60 Hz): render event + slot copy + publish.
const VIDEO_INTERVAL: Duration = Duration::from_millis(16);

/// Audio pull cadence; each pull covers the wall-clock elapsed since the
/// previous one so serve-loop stalls (video blits) don't starve the stream.
const AUDIO_INTERVAL: Duration = Duration::from_millis(10);

/// Upper bound on one pull's catch-up so a long stall can't burst-allocate.
const AUDIO_MAX_PULL: Duration = Duration::from_millis(100);

/// Socket poll granularity for the single-threaded serve loop.
const POLL_TIMEOUT_MS: i64 = 5;

/// The core's callbacks fire from arbitrary FFmpeg/player threads; they
/// funnel into this channel and the serve loop forwards them on the socket.
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
    socket: &zmq::Socket,
    #[cfg(target_os = "macos")] service: &str,
    #[cfg(target_os = "windows")] parent_pid: u32,
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

    loop {
        socket::poll_readable(socket, POLL_TIMEOUT_MS)?;

        while let Some(message) = socket::try_recv::<ToServer>(socket)? {
            if dispatch(
                socket,
                message,
                &mut serve,
                #[cfg(target_os = "macos")]
                service,
                #[cfg(target_os = "windows")]
                parent_pid,
            )? {
                core::uuav_deinit();
                return Ok(());
            }
        }

        forward_logs(socket, &rx)?;

        if last_pump.elapsed() >= PUMP_INTERVAL {
            last_pump = Instant::now();
            pump_states(socket, &mut serve.players)?;
        }

        if last_audio.elapsed() >= AUDIO_INTERVAL {
            let elapsed = last_audio.elapsed().min(AUDIO_MAX_PULL);
            last_audio = Instant::now();
            pump_audio(socket, &mut serve, elapsed)?;
        }

        if last_video.elapsed() >= VIDEO_INTERVAL {
            last_video = Instant::now();
            if let Some(video) = serve.video.as_mut() {
                video.tick(serve.players.keys().copied(), socket);
            }
        }
    }
}

/// Returns `true` on `Shutdown`.
fn dispatch(
    socket: &zmq::Socket,
    message: ToServer,
    serve: &mut Serve,
    #[cfg(target_os = "macos")] service: &str,
    #[cfg(target_os = "windows")] parent_pid: u32,
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
                let pump = crate::video::VideoPump::new(probe, parent_pid);
                serve.video = Some(pump.map_err(|e| e.to_string())?);
                Ok(())
            });
            reply(socket, corr, result.map(|()| ReplyBody::Unit))?;
        }
        ToServer::SetLogLevel { level } => core::uuav_set_log_level(level),
        ToServer::UpdateAudioOut { corr, audio } => {
            let result = state::consume_result(core::uuav_update_audio_out(
                core::AudioOptionsRaw {
                    sample_rate: audio.sample_rate,
                    channels: audio.channels,
                },
            ));
            if result.is_ok() {
                serve.audio = Some(audio);
            }
            reply(socket, corr, result.map(|()| ReplyBody::Unit))?;
        }
        ToServer::PlayerNew { corr } => {
            let result = player_new(players);
            reply(socket, corr, result.map(ReplyBody::PlayerId))?;
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
            report_command_error(socket, id, "open media", outcome)?;
        }
        ToServer::CloseMedia { id } => {
            let outcome = state::consume_result(core::uuav_player_close_media(id));
            report_command_error(socket, id, "close media", outcome)?;
        }
        ToServer::Play { id } => {
            let outcome = state::consume_result(core::uuav_player_play(id));
            report_command_error(socket, id, "play", outcome)?;
        }
        ToServer::Pause { id } => {
            let outcome = state::consume_result(core::uuav_player_pause(id));
            report_command_error(socket, id, "pause", outcome)?;
        }
        ToServer::Seek { id, time } => {
            let outcome = state::consume_result(core::uuav_player_seek_async(id, time));
            report_command_error(socket, id, "seek", outcome)?;
        }
        ToServer::SetLooping { id, looping } => {
            let outcome =
                state::consume_result(core::uuav_player_set_looping(id, u8::from(looping)));
            report_command_error(socket, id, "set looping", outcome)?;
        }
        ToServer::SetRate { id, rate } => {
            let outcome = state::consume_result(core::uuav_player_set_rate(id, rate));
            report_command_error(socket, id, "set rate", outcome)?;
        }
        ToServer::AssignMasterClock { id, time } => {
            let outcome =
                state::consume_result(core::uuav_player_assign_master_clock(id, time));
            report_command_error(socket, id, "assign master clock", outcome)?;
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
    socket: &zmq::Socket,
    id: PlayerId,
    command: &str,
    outcome: Result<(), String>,
) -> anyhow::Result<()> {
    if let Err(message) = outcome {
        socket::send(
            socket,
            &ToClient::PlayerError {
                id: Some(id),
                message: format!("uuav {command} failed: {message}"),
            },
        )
        .context("send PlayerError")?;
    }
    Ok(())
}

fn reply(
    socket: &zmq::Socket,
    corr: u32,
    result: Result<ReplyBody, String>,
) -> anyhow::Result<()> {
    socket::send(socket, &ToClient::Reply { corr, result }).context("send Reply")
}

fn forward_logs(socket: &zmq::Socket, rx: &Receiver<(LogSink, String)>) -> anyhow::Result<()> {
    while let Ok((sink, line)) = rx.try_recv() {
        socket::send(socket, &ToClient::Log { sink, line }).context("send Log")?;
    }
    Ok(())
}

/// Pulls the elapsed wall-clock worth of samples from every player and
/// forwards non-empty reads; the pull cadence stands in for Unity's audio
/// thread, so the core's own drift correction keeps running unchanged.
fn pump_audio(
    socket: &zmq::Socket,
    serve: &mut Serve,
    elapsed: Duration,
) -> anyhow::Result<()> {
    let Some(audio) = serve.audio else {
        return Ok(());
    };
    let frames = (f64::from(audio.sample_rate) * elapsed.as_secs_f64()) as i32;
    if frames <= 0 {
        return Ok(());
    }
    let samples = frames as usize * audio.channels.max(1) as usize;
    serve.audio_scratch.resize(samples, 0.0);

    for &id in serve.players.keys() {
        let read = unsafe {
            core::uuav_player_read_audio(id, serve.audio_scratch.as_mut_ptr(), frames)
        };
        if read <= 0 {
            continue;
        }
        let filled = read as usize * audio.channels.max(1) as usize;
        let Some(payload) = serve.audio_scratch.get(..filled) else {
            continue;
        };
        socket::send(
            socket,
            &ToClient::AudioPacket {
                id,
                samples: payload.to_vec(),
            },
        )?;
    }
    Ok(())
}

fn pump_states(
    socket: &zmq::Socket,
    players: &mut HashMap<PlayerId, PlayerTracking>,
) -> anyhow::Result<()> {
    for (&id, tracking) in players.iter_mut() {
        socket::send(socket, &ToClient::State(state::snapshot(id))).context("send State")?;

        if !tracking.media_info_sent
            && let Some(info) = state::media_info(id)
        {
            tracking.media_info_sent = true;
            socket::send(socket, &ToClient::MediaInfo { id, info })
                .context("send MediaInfo")?;
        }
    }
    Ok(())
}
