
#[cfg(target_os = "macos")]
mod imp {
    use std::ffi::CStr;
    use std::sync::Arc;
    use std::sync::atomic::{AtomicBool, Ordering};
    use std::thread::{self, JoinHandle};
    use std::time::Duration;

    use anyhow::Result;
    use uuav_ipc::mach_ipc::{self, SendRight};
    use uuav_ipc::protocol::{AUDIO_PACKET_SAMPLES, LogLevel, SharedSegment, uptime_nanos};

    const TICK: Duration = Duration::from_millis(10);
    const TICK_NANOS: u64 = 10_000_000;

    const PACKETS_PER_TICK: u32 = 4;

    const SEND_TIMEOUT_MS: u32 = 0;

    const RESYNC_TOLERANCE: f64 = 0.050;

    const PLAYER_PROBE_LIMIT: u64 = 8;

    const TRACE_ENV: &str = "UUAV_AUDIO_TRACE";

    const UUAV_UNKNOWN: i32 = 7;

    pub struct Pump {
        stop: Arc<AtomicBool>,
        handle: Option<JoinHandle<()>>,
    }

    impl Pump {
        pub fn start(segment: &SharedSegment, service: &CStr) -> Option<Self> {
            match Self::try_start(segment, service) {
                Ok(pump) => Some(pump),
                Err(error) => {
                    segment.log.emit(
                        LogLevel::Error,
                        &format!("the audio pump did not start, so this player is silent: {error:#}"),
                    );
                    None
                }
            }
        }

        fn try_start(segment: &SharedSegment, service: &CStr) -> Result<Self> {
            let host = mach_ipc::look_up(service)?;
            let stop = Arc::new(AtomicBool::new(false));
            let stop_for_thread = Arc::clone(&stop);
            let segment = SegmentPtr(std::ptr::from_ref(segment));
            let trace = std::env::var_os(TRACE_ENV).is_some();

            let handle = thread::Builder::new()
                .name("uuav-adapter-audio".to_owned())
                .spawn(move || {
                    let segment = segment;
                    let segment = unsafe { &*segment.0 };
                    run(segment, &host, &stop_for_thread, trace);
                })?;

            Ok(Self {
                stop,
                handle: Some(handle),
            })
        }
    }

    impl Drop for Pump {
        fn drop(&mut self) {
            self.stop.store(true, Ordering::Release);
            if let Some(handle) = self.handle.take() {
                let _ = handle.join();
            }
        }
    }

    #[derive(Clone, Copy)]
    struct SegmentPtr(*const SharedSegment);

    unsafe impl Send for SegmentPtr {}

    struct Timeline {
        anchor: Option<f64>,
        frames_since_anchor: u64,
        discontinuous: bool,
        last_pull_nanos: u64,
        wakeups: u64,
        packets: u64,
        frames: u64,
        gaps: u64,
        drops: u64,
        first_pts: f64,
        last_pts: f64,
    }

    impl Timeline {
        const fn new(now_nanos: u64) -> Self {
            Self {
                anchor: None,
                frames_since_anchor: 0,
                discontinuous: true,
                last_pull_nanos: now_nanos,
                wakeups: 0,
                packets: 0,
                frames: 0,
                gaps: 0,
                drops: 0,
                first_pts: f64::NAN,
                last_pts: f64::NAN,
            }
        }
    }

    fn run(segment: &SharedSegment, host: &SendRight, stop: &AtomicBool, trace: bool) {
        let Some(player) = await_player(stop) else {
            return;
        };
        if trace {
            segment.log.emit(
                LogLevel::Info,
                &format!("audio pump attached to core player {player}"),
            );
        }

        let mut buffer = [0.0f32; AUDIO_PACKET_SAMPLES];
        let mut timeline = Timeline::new(uptime_nanos());
        let mut next_report = uptime_nanos().saturating_add(1_000_000_000);
        let mut next_tick = uptime_nanos();

        while !stop.load(Ordering::Acquire) {
            if segment.cancel.is_set() {
                break;
            }
            let Some((sample_rate, channels, _generation)) = segment.audio_options.read() else {
                thread::sleep(TICK);
                continue;
            };
            tick(
                player,
                host,
                &mut buffer,
                &mut timeline,
                sample_rate,
                channels,
            );

            if trace {
                let now = uptime_nanos();
                if now >= next_report {
                    next_report = now.saturating_add(1_000_000_000);
                    segment.log.emit(
                        LogLevel::Info,
                        &format!(
                            "audio: wakeups={} packets={} frames={} pts=[{:.3}..{:.3}] gaps={} \
                             dropped={}",
                            timeline.wakeups,
                            timeline.packets,
                            timeline.frames,
                            timeline.first_pts,
                            timeline.last_pts,
                            timeline.gaps,
                            timeline.drops,
                        ),
                    );
                }
            }

            let now = uptime_nanos();
            next_tick = next_tick.saturating_add(TICK_NANOS).max(now);
            thread::sleep(Duration::from_nanos(next_tick.saturating_sub(now)));
        }
    }

    fn tick(
        player: u64,
        host: &SendRight,
        buffer: &mut [f32; AUDIO_PACKET_SAMPLES],
        timeline: &mut Timeline,
        sample_rate: u32,
        channels: u32,
    ) {
        let Some(channels) = usize::try_from(channels).ok().filter(|value| *value > 0) else {
            return;
        };
        let per_packet = AUDIO_PACKET_SAMPLES.checked_div(channels).unwrap_or(0);
        if per_packet == 0 || sample_rate == 0 {
            return;
        }

        timeline.wakeups = timeline.wakeups.wrapping_add(1);
        let now = uptime_nanos();
        let elapsed = now.saturating_sub(timeline.last_pull_nanos);
        let earned = elapsed
            .saturating_mul(u64::from(sample_rate))
            .checked_div(1_000_000_000)
            .unwrap_or(0);
        if earned == 0 {
            return;
        }
        let consumed = earned
            .saturating_mul(1_000_000_000)
            .checked_div(u64::from(sample_rate))
            .unwrap_or(0);
        timeline.last_pull_nanos = timeline.last_pull_nanos.saturating_add(consumed).min(now);

        let mut budget = earned;
        for _ in 0..PACKETS_PER_TICK {
            let want = usize::try_from(budget).unwrap_or(per_packet).min(per_packet);
            if want == 0 {
                break;
            }
            let Ok(request) = i32::try_from(want) else {
                break;
            };
            let produced = unsafe { uuav::uuav_player_read_audio(player, buffer.as_mut_ptr(), request) };
            let Some(frames) = usize::try_from(produced).ok().filter(|value| *value > 0) else {
                break;
            };
            budget = budget.saturating_sub(frames as u64);

            let samples = frames.saturating_mul(channels);
            let Some(payload) = buffer.get(..samples) else {
                break;
            };
            let (pts, discontinuous) = stamp(player, timeline, frames as u64, sample_rate);
            if mach_ipc::send_audio(host, pts, payload, discontinuous, SEND_TIMEOUT_MS).is_err() {
                timeline.drops = timeline.drops.wrapping_add(1);
                timeline.discontinuous = true;
                break;
            }
            timeline.packets = timeline.packets.wrapping_add(1);
            timeline.frames = timeline.frames.wrapping_add(frames as u64);
            if timeline.first_pts.is_nan() {
                timeline.first_pts = pts;
            }
            timeline.last_pts = pts;
            if discontinuous && timeline.packets > 1 {
                timeline.gaps = timeline.gaps.wrapping_add(1);
            }
            if frames < want {
                break;
            }
        }
    }

    fn stamp(player: u64, timeline: &mut Timeline, frames: u64, sample_rate: u32) -> (f64, bool) {
        let media = current_time(player);
        let seconds = f64::from(sample_rate);

        let (pts, broke) = match timeline.anchor {
            Some(anchor) => {
                let derived = anchor + timeline.frames_since_anchor as f64 / seconds;
                match media.filter(|value| (derived - value).abs() > RESYNC_TOLERANCE) {
                    Some(resynced) => {
                        timeline.anchor = Some(resynced);
                        timeline.frames_since_anchor = 0;
                        (resynced, true)
                    }
                    None => (derived, false),
                }
            }
            None => {
                let base = media.unwrap_or(0.0);
                timeline.anchor = Some(base);
                timeline.frames_since_anchor = 0;
                (base, true)
            }
        };
        timeline.frames_since_anchor = timeline.frames_since_anchor.wrapping_add(frames);

        let discontinuous = broke || timeline.discontinuous;
        timeline.discontinuous = false;
        (pts, discontinuous)
    }

    fn current_time(player: u64) -> Option<f64> {
        let mut seconds = 0.0_f64;
        let result = unsafe { uuav::uuav_player_current_time(player, &raw mut seconds) };
        if result.error_message.is_null() {
            return seconds.is_finite().then_some(seconds);
        }
        unsafe { uuav::uuav_string_free(result.error_message.cast_mut()) };
        None
    }

    fn await_player(stop: &AtomicBool) -> Option<u64> {
        while !stop.load(Ordering::Acquire) {
            for candidate in 1..=PLAYER_PROBE_LIMIT {
                if uuav::uuav_player_state(candidate) as i32 != UUAV_UNKNOWN {
                    return Some(candidate);
                }
            }
            thread::sleep(TICK);
        }
        None
    }
}

#[cfg(windows)]
mod imp {
    use std::sync::Arc;
    use std::sync::atomic::{AtomicBool, Ordering};
    use std::thread::{self, JoinHandle};
    use std::time::Duration;

    use anyhow::Result;
    use uuav_ipc::protocol::{AUDIO_PACKET_SAMPLES, LogLevel, SharedSegment, uptime_nanos};
    use uuav_ipc::win::bridge::Producer;

    const TICK: Duration = Duration::from_millis(10);
    const TICK_NANOS: u64 = 10_000_000;

    const PACKETS_PER_TICK: u32 = 4;

    const RESYNC_TOLERANCE: f64 = 0.050;

    const PLAYER_PROBE_LIMIT: u64 = 8;

    const TRACE_ENV: &str = "UUAV_AUDIO_TRACE";

    const UUAV_UNKNOWN: i32 = 7;

    pub struct Pump {
        stop: Arc<AtomicBool>,
        handle: Option<JoinHandle<()>>,
    }

    impl Pump {
        pub fn start(segment: &SharedSegment) -> Option<Self> {
            match Self::try_start(segment) {
                Ok(pump) => Some(pump),
                Err(error) => {
                    segment.log.emit(
                        LogLevel::Error,
                        &format!("the audio pump did not start, so this player is silent: {error:#}"),
                    );
                    None
                }
            }
        }

        fn try_start(segment: &SharedSegment) -> Result<Self> {
            let stop = Arc::new(AtomicBool::new(false));
            let stop_for_thread = Arc::clone(&stop);
            let segment = SegmentPtr(std::ptr::from_ref(segment));
            let trace = std::env::var_os(TRACE_ENV).is_some();

            let handle = thread::Builder::new()
                .name("uuav-adapter-audio".to_owned())
                .spawn(move || {
                    let segment = segment;
                    let segment = unsafe { &*segment.0 };
                    run(segment, &stop_for_thread, trace);
                })?;

            Ok(Self {
                stop,
                handle: Some(handle),
            })
        }
    }

    impl Drop for Pump {
        fn drop(&mut self) {
            self.stop.store(true, Ordering::Release);
            if let Some(handle) = self.handle.take() {
                let _ = handle.join();
            }
        }
    }

    #[derive(Clone, Copy)]
    struct SegmentPtr(*const SharedSegment);

    unsafe impl Send for SegmentPtr {}

    struct Timeline {
        anchor: Option<f64>,
        frames_since_anchor: u64,
        discontinuous: bool,
        last_pull_nanos: u64,
        wakeups: u64,
        packets: u64,
        frames: u64,
        gaps: u64,
        drops: u64,
        first_pts: f64,
        last_pts: f64,
    }

    impl Timeline {
        const fn new(now_nanos: u64) -> Self {
            Self {
                anchor: None,
                frames_since_anchor: 0,
                discontinuous: true,
                last_pull_nanos: now_nanos,
                wakeups: 0,
                packets: 0,
                frames: 0,
                gaps: 0,
                drops: 0,
                first_pts: f64::NAN,
                last_pts: f64::NAN,
            }
        }
    }

    fn run(segment: &SharedSegment, stop: &AtomicBool, trace: bool) {
        let Some(player) = await_player(stop) else {
            return;
        };
        if trace {
            segment.log.emit(
                LogLevel::Info,
                &format!("audio pump attached to core player {player}"),
            );
        }

        let mut producer = Producer::new();
        let mut buffer = [0.0f32; AUDIO_PACKET_SAMPLES];
        let mut timeline = Timeline::new(uptime_nanos());
        let mut next_report = uptime_nanos().saturating_add(1_000_000_000);
        let mut next_tick = uptime_nanos();

        while !stop.load(Ordering::Acquire) {
            if segment.cancel.is_set() {
                break;
            }
            let Some((sample_rate, channels, generation)) = segment.audio_options.read() else {
                thread::sleep(TICK);
                continue;
            };
            tick(
                player,
                segment,
                &mut producer,
                &mut buffer,
                &mut timeline,
                sample_rate,
                channels,
                generation,
            );

            if trace {
                let now = uptime_nanos();
                if now >= next_report {
                    next_report = now.saturating_add(1_000_000_000);
                    segment.log.emit(
                        LogLevel::Info,
                        &format!(
                            "audio: wakeups={} packets={} frames={} pts=[{:.3}..{:.3}] gaps={} \
                             dropped={}",
                            timeline.wakeups,
                            timeline.packets,
                            timeline.frames,
                            timeline.first_pts,
                            timeline.last_pts,
                            timeline.gaps,
                            timeline.drops,
                        ),
                    );
                }
            }

            let now = uptime_nanos();
            next_tick = next_tick.saturating_add(TICK_NANOS).max(now);
            thread::sleep(Duration::from_nanos(next_tick.saturating_sub(now)));
        }
    }

    #[allow(
        clippy::too_many_arguments,
        reason = "the mach twin threads the same values; a struct would only \
                  rename the coupling"
    )]
    fn tick(
        player: u64,
        segment: &SharedSegment,
        producer: &mut Producer,
        buffer: &mut [f32; AUDIO_PACKET_SAMPLES],
        timeline: &mut Timeline,
        sample_rate: u32,
        channels: u32,
        options_generation: u64,
    ) {
        let Some(width) = usize::try_from(channels).ok().filter(|value| *value > 0) else {
            return;
        };
        let per_packet = AUDIO_PACKET_SAMPLES.checked_div(width).unwrap_or(0);
        if per_packet == 0 || sample_rate == 0 {
            return;
        }

        timeline.wakeups = timeline.wakeups.wrapping_add(1);
        let now = uptime_nanos();
        let elapsed = now.saturating_sub(timeline.last_pull_nanos);
        let earned = elapsed
            .saturating_mul(u64::from(sample_rate))
            .checked_div(1_000_000_000)
            .unwrap_or(0);
        if earned == 0 {
            return;
        }
        let consumed = earned
            .saturating_mul(1_000_000_000)
            .checked_div(u64::from(sample_rate))
            .unwrap_or(0);
        timeline.last_pull_nanos = timeline.last_pull_nanos.saturating_add(consumed).min(now);

        let mut budget = earned;
        for _ in 0..PACKETS_PER_TICK {
            let want = usize::try_from(budget).unwrap_or(per_packet).min(per_packet);
            if want == 0 {
                break;
            }
            let Ok(request) = i32::try_from(want) else {
                break;
            };
            let pulled = unsafe { uuav::uuav_player_read_audio(player, buffer.as_mut_ptr(), request) };
            let Some(frames) = usize::try_from(pulled).ok().filter(|value| *value > 0) else {
                break;
            };
            budget = budget.saturating_sub(frames as u64);

            let samples = frames.saturating_mul(width);
            let Some(payload) = buffer.get(..samples) else {
                break;
            };
            let (pts, discontinuous) = stamp(player, timeline, frames as u64, sample_rate);
            let accepted = producer.push_packet(
                segment,
                sample_rate,
                channels,
                options_generation,
                pts,
                payload,
                discontinuous,
            );
            if accepted < payload.len() {
                timeline.drops = timeline.drops.wrapping_add(1);
                timeline.discontinuous = true;
                break;
            }
            timeline.packets = timeline.packets.wrapping_add(1);
            timeline.frames = timeline.frames.wrapping_add(frames as u64);
            if timeline.first_pts.is_nan() {
                timeline.first_pts = pts;
            }
            timeline.last_pts = pts;
            if discontinuous && timeline.packets > 1 {
                timeline.gaps = timeline.gaps.wrapping_add(1);
            }
            if frames < want {
                break;
            }
        }
    }

    fn stamp(player: u64, timeline: &mut Timeline, frames: u64, sample_rate: u32) -> (f64, bool) {
        let media = current_time(player);
        let seconds = f64::from(sample_rate);

        let (pts, broke) = match timeline.anchor {
            Some(anchor) => {
                let derived = anchor + timeline.frames_since_anchor as f64 / seconds;
                match media.filter(|value| (derived - value).abs() > RESYNC_TOLERANCE) {
                    Some(resynced) => {
                        timeline.anchor = Some(resynced);
                        timeline.frames_since_anchor = 0;
                        (resynced, true)
                    }
                    None => (derived, false),
                }
            }
            None => {
                let base = media.unwrap_or(0.0);
                timeline.anchor = Some(base);
                timeline.frames_since_anchor = 0;
                (base, true)
            }
        };
        timeline.frames_since_anchor = timeline.frames_since_anchor.wrapping_add(frames);

        let discontinuous = broke || timeline.discontinuous;
        timeline.discontinuous = false;
        (pts, discontinuous)
    }

    fn current_time(player: u64) -> Option<f64> {
        let mut seconds = 0.0_f64;
        let result = unsafe { uuav::uuav_player_current_time(player, &raw mut seconds) };
        if result.error_message.is_null() {
            return seconds.is_finite().then_some(seconds);
        }
        unsafe { uuav::uuav_string_free(result.error_message.cast_mut()) };
        None
    }

    fn await_player(stop: &AtomicBool) -> Option<u64> {
        while !stop.load(Ordering::Acquire) {
            for candidate in 1..=PLAYER_PROBE_LIMIT {
                if uuav::uuav_player_state(candidate) as i32 != UUAV_UNKNOWN {
                    return Some(candidate);
                }
            }
            thread::sleep(TICK);
        }
        None
    }
}

pub use imp::Pump;
