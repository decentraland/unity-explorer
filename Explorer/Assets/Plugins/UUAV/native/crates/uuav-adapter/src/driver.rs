
use anyhow::{Result, anyhow};
use uuav_ipc::protocol::{
    ClockWire, LogLevel, MediaFactsValue, PlaybackState, SharedSegment, TransportSnapshot, error,
    kind, uptime_nanos,
};

use crate::core::Core;
use crate::gpu::{self, SurfaceHandle};

pub const MAX_SERVICE_HZ: u32 = 144;

pub const PUMP_SLICE_MS: u32 = 1000_u32.div_ceil(MAX_SERVICE_HZ);

const COMMANDS_PER_TICK: u32 = 16;

mod uuav_state {
    pub const CLOSED: u32 = 0;
    pub const OPENING: u32 = 1;
    pub const READY: u32 = 2;
    pub const PLAYING: u32 = 3;
    pub const PAUSED: u32 = 4;
    pub const ENDED: u32 = 5;
    pub const ERROR: u32 = 6;
}

const CLOCK_CONTINUITY: f64 = 0.001;

const DEFAULT_ENGINE_FORMAT: (u32, u32) = (48_000, 2);

pub struct Incoming {
    pub kind: u32,
    pub payload: u64,
}

pub trait Channel {
    fn receive(&mut self, timeout_ms: u32) -> Result<Option<Incoming>>;

    fn send(&mut self, kind: u32, index: u32, payload: u64) -> Result<()>;

    fn send_surface(
        &mut self,
        index: u32,
        generation: u64,
        _surface: SurfaceHandle,
    ) -> Result<()> {
        Err(anyhow!(
            "this transport carries no surfaces, so slot {index} generation {generation} cannot \
             be handed over"
        ))
    }

    fn host_is_gone(&mut self) -> bool;
}

impl<C: Channel + ?Sized> Channel for &mut C {
    fn receive(&mut self, timeout_ms: u32) -> Result<Option<Incoming>> {
        (**self).receive(timeout_ms)
    }

    fn send(&mut self, kind: u32, index: u32, payload: u64) -> Result<()> {
        (**self).send(kind, index, payload)
    }

    fn send_surface(&mut self, index: u32, generation: u64, surface: SurfaceHandle) -> Result<()> {
        (**self).send_surface(index, generation, surface)
    }

    fn host_is_gone(&mut self) -> bool {
        (**self).host_is_gone()
    }
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum Reported {
    Nothing,
    Open,
    Ended,
    Failed,
}

pub struct Driver<'a, C: Channel> {
    segment: &'a SharedSegment,
    channel: C,
    core: Core,
    reported: Reported,
    open_generation: u64,
    clock: ClockWire,
    facts: Option<MediaFactsValue>,
    rate_generation: u64,
    applied_looping: bool,
    echoed: (bool, u64),
    audio_options_generation: u64,
    frames: Option<gpu::Frames>,
    frames_reported: bool,
    shutdown: bool,
}

impl<'a, C: Channel> Driver<'a, C> {
    pub fn start(segment: &'a SharedSegment, mut channel: C, luid: Option<u64>) -> Result<Self> {
        let published = segment.audio_options.read();
        let engine = published.map_or(DEFAULT_ENGINE_FORMAT, |(rate, channels, _)| (rate, channels));
        let audio_options_generation = published.map_or(0, |(_, _, generation)| generation);

        let brought_up = Self::bring_up(segment, luid, engine);
        let core = match brought_up {
            Ok(core) => core,
            Err(failure) => {
                segment.log.emit(
                    LogLevel::Error,
                    &format!("the media core could not start: {failure:#}"),
                );
                let _ = channel.send(kind::FAILED, 0, error::OPEN_FAILED);
                return Err(failure);
            }
        };

        let frames = gpu::Frames::start(segment);

        Ok(Self {
            segment,
            channel,
            core,
            reported: Reported::Nothing,
            open_generation: 0,
            clock: ClockWire::HELD_AT_ZERO,
            facts: None,
            rate_generation: 0,
            applied_looping: false,
            echoed: (false, 0),
            audio_options_generation,
            frames,
            frames_reported: false,
            shutdown: false,
        })
    }

    fn bring_up(segment: &SharedSegment, luid: Option<u64>, engine: (u32, u32)) -> Result<Core> {
        let whitelist = segment
            .protocol_whitelist
            .read()
            .ok_or_else(|| anyhow!("the host published no protocol whitelist"))?;
        let probe = crate::probe::Probe::create(luid)?;
        let device = probe.describe();
        let core = Core::start(probe, engine, &whitelist)?;
        segment.log.emit(
            LogLevel::Info,
            &format!(
                "uuav-adapter drives the media core on {device}: engine audio {}Hz x{}, protocols \
                 {whitelist}",
                engine.0, engine.1
            ),
        );
        Ok(core)
    }

    pub fn run(&mut self) -> Result<()> {
        self.publish_transport(PlaybackState::Ready, ClockWire::HELD_AT_ZERO);
        loop {
            if self.segment.cancel.is_set() || self.channel.host_is_gone() {
                return Ok(());
            }
            self.pump_commands()?;
            if self.shutdown {
                return Ok(());
            }
            self.service_controls();
            self.service_audio_options();
            self.core.on_render_event();
            self.service_frames();
            self.publish_echo();
            self.publish()?;
        }
    }

    fn pump_commands(&mut self) -> Result<()> {
        let mut timeout = PUMP_SLICE_MS;
        for _ in 0..COMMANDS_PER_TICK {
            let Some(incoming) = self.channel.receive(timeout)? else {
                return Ok(());
            };
            timeout = 0;
            if !uuav_ipc::protocol::is_host_to_helper(incoming.kind) {
                self.log(
                    LogLevel::Warning,
                    &format!("ignoring message kind {:#x}", incoming.kind),
                );
                continue;
            }
            match incoming.kind {
                kind::OPEN => self.on_open(incoming.payload)?,
                kind::PLAY => self.report(self.core.play(), "play"),
                kind::PAUSE => self.report(self.core.pause(), "pause"),
                kind::CLOSE => self.on_close(),
                kind::SET_LOG_LEVEL => Core::set_log_level(incoming.payload.cast_signed() as i32),
                kind::SHUTDOWN => {
                    self.shutdown = true;
                    return Ok(());
                }
                _ => {}
            }
        }
        Ok(())
    }

    fn on_open(&mut self, generation: u64) -> Result<()> {
        let Some(url) = self.segment.open.take(generation) else {
            self.log(
                LogLevel::Info,
                "open request superseded before it was serviced",
            );
            return Ok(());
        };
        self.open_generation = generation;
        self.reported = Reported::Nothing;
        self.facts = None;
        self.clock = ClockWire::HELD_AT_ZERO;

        if let Err(failure) = self.core.open(&url) {
            self.log(LogLevel::Error, &format!("open failed: {failure:#}"));
            self.reported = Reported::Failed;
            self.channel.send(kind::FAILED, 0, error::OPEN_FAILED)?;
        }
        Ok(())
    }

    fn on_close(&mut self) {
        self.report(self.core.close(), "close_media");
        self.reported = Reported::Nothing;
        self.facts = None;
        self.publish_transport(PlaybackState::Ready, ClockWire::HELD_AT_ZERO);
    }

    fn service_controls(&mut self) {
        let (rate, rate_generation) = self.segment.controls.requested_rate();
        if rate_generation != self.rate_generation {
            self.report(self.core.set_rate(rate), "set_rate");
            self.rate_generation = rate_generation;
        }

        let looping = self.segment.controls.looping();
        if looping != self.applied_looping {
            self.report(self.core.set_looping(looping), "set_looping");
            self.applied_looping = looping;
        }

        if let Some(target) = self.segment.seek.take() {
            self.report(self.core.seek(target), "seek_async");
        }
    }

    fn publish_echo(&mut self) {
        let Some(controls) = self.core.controls_state() else {
            return;
        };
        let looping = if controls.looping_pending == 0 {
            controls.looping != 0
        } else {
            self.echoed.0
        };
        let rate_generation = if controls.rate_pending == 0 {
            self.rate_generation
        } else {
            self.echoed.1
        };
        if (looping, rate_generation) == self.echoed {
            return;
        }
        self.echoed = (looping, rate_generation);
        self.segment.controls_echo.publish(looping, rate_generation);
    }

    fn service_audio_options(&mut self) {
        let Some((sample_rate, channels, generation)) = self.segment.audio_options.read() else {
            return;
        };
        if generation == self.audio_options_generation {
            return;
        }
        self.audio_options_generation = generation;
        self.report(
            Core::update_audio_out(sample_rate, channels),
            "update_audio_out",
        );
    }

    fn service_frames(&mut self) {
        let Self {
            frames,
            core,
            channel,
            ..
        } = self;
        let outcome = gpu::service(frames, core, &mut |index, generation, surface| {
            channel.send_surface(index, generation, surface)
        });
        let Err(failure) = outcome else {
            return;
        };
        self.frames = None;
        if !self.frames_reported {
            self.frames_reported = true;
            self.log(
                LogLevel::Error,
                &format!("GPU frame delivery stopped: {failure:#}"),
            );
        }
    }

    fn publish(&mut self) -> Result<()> {
        let raw = self.core.state();
        self.publish_facts(raw);

        match raw {
            uuav_state::READY | uuav_state::PLAYING | uuav_state::PAUSED => {
                let playing = raw == uuav_state::PLAYING;
                let state = match raw {
                    uuav_state::PLAYING => PlaybackState::Playing,
                    uuav_state::PAUSED => PlaybackState::Paused,
                    _ => PlaybackState::Ready,
                };
                let clock = self.sample_clock(playing);
                self.publish_transport(state, clock);
                if self.reported != Reported::Open {
                    self.reported = Reported::Open;
                    self.channel.send(kind::OPENED, 0, self.open_generation)?;
                }
            }
            uuav_state::ENDED => {
                let clock = self.sample_clock(false);
                self.publish_transport(PlaybackState::Ended, clock);
                if self.reported != Reported::Ended {
                    self.reported = Reported::Ended;
                    self.channel.send(kind::ENDED, 0, 0)?;
                }
            }
            uuav_state::ERROR => {
                if self.reported != Reported::Failed {
                    let code = if self.reported == Reported::Open {
                        error::DECODE_FAILED
                    } else {
                        error::OPEN_FAILED
                    };
                    self.reported = Reported::Failed;
                    self.channel.send(kind::FAILED, 0, code)?;
                }
            }
            uuav_state::CLOSED => self.reported = Reported::Nothing,
            _ => {}
        }
        Ok(())
    }

    fn sample_clock(&self, playing: bool) -> ClockWire {
        let anchor = uptime_nanos();
        let extrapolated = self.clock.now(anchor);
        let media = self.core.current_time().unwrap_or(extrapolated);
        let skew = extrapolated - media;
        let base = if skew > 0.0 && skew < CLOCK_CONTINUITY {
            extrapolated
        } else {
            media
        };
        ClockWire {
            base,
            anchor_nanos: if playing { anchor } else { 0 },
            rate: self.core.rate(),
        }
    }

    fn publish_transport(&mut self, state: PlaybackState, clock: ClockWire) {
        self.clock = clock;
        self.segment
            .transport
            .publish(TransportSnapshot { state, clock });
    }

    fn publish_facts(&mut self, raw: u32) {
        if matches!(raw, uuav_state::CLOSED | uuav_state::OPENING) {
            return;
        }
        let Some(facts) = self.core.facts(self.open_generation) else {
            return;
        };
        if self.facts == Some(facts) {
            return;
        }
        self.facts = Some(facts);
        self.segment.media.publish(facts);
        if let Some(summary) = self.core.stream_summary() {
            self.log(LogLevel::Info, &summary);
        }
    }

    fn report(&self, outcome: Result<()>, what: &str) {
        if let Err(failure) = outcome {
            self.log(LogLevel::Warning, &format!("{what}: {failure:#}"));
        }
    }

    fn log(&self, level: LogLevel, text: &str) {
        self.segment.log.emit(level, text);
    }
}
