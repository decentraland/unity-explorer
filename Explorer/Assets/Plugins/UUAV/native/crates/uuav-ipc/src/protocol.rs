
use std::ffi::{CString, NulError};
use std::fmt;
use std::sync::atomic::{AtomicI64, AtomicU8, AtomicU32, AtomicU64, Ordering, fence};

pub const PROTOCOL_MAGIC: u64 = 0x5555_4156_5f49_5043;

pub const PROTOCOL_VERSION: u32 = 2;

pub const SERVICE_NAME_PREFIX: &str = "com.decentraland.uuav.helper";

pub const SEGMENT_NAME_PREFIX: &str = "/uuav.";

pub const SHM_NAME_MAX: usize = 31;

pub const VIDEO_RING_CAPACITY: usize = 8;

pub const RELEASE_RING_CAPACITY: usize = 16;

pub const RETAINED_FRAMES: usize = 4;

pub const SURFACE_SLOT_COUNT: usize = 32;

const _: () = assert!(SURFACE_SLOT_COUNT > 8 + VIDEO_RING_CAPACITY + RETAINED_FRAMES);

pub const AUDIO_SAMPLE_CAPACITY: usize = 1 << 19;

pub const AUDIO_MARKER_CAPACITY: usize = 256;

pub const AUDIO_PACKET_SAMPLES: usize = 1024;

pub const LOG_RING_CAPACITY: usize = 32;

pub const LOG_TEXT_BYTES: usize = 248;

pub const URL_MAX_BYTES: usize = 2048;

pub const FETCH_SLOT_BYTES: usize = 128 * 1024;

pub const MAX_PLANE_DIMENSION: u32 = 16_384;

pub const SEQLOCK_READ_ATTEMPTS: u32 = 16;

const _: () = assert!(VIDEO_RING_CAPACITY.is_power_of_two());
const _: () = assert!(RELEASE_RING_CAPACITY.is_power_of_two());
const _: () = assert!(SURFACE_SLOT_COUNT.is_power_of_two());
const _: () = assert!(AUDIO_SAMPLE_CAPACITY.is_power_of_two());
const _: () = assert!(AUDIO_MARKER_CAPACITY.is_power_of_two());
const _: () = assert!(LOG_RING_CAPACITY.is_power_of_two());
const _: () = assert!(RELEASE_RING_CAPACITY > VIDEO_RING_CAPACITY + RETAINED_FRAMES);

const fn ring_slot(index: u64, capacity: usize) -> usize {
    (index & (capacity as u64).wrapping_sub(1)) as usize
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Fault {
    Ring {
        region: &'static str,
        head: u64,
        tail: u64,
        capacity: u64,
    },
    Frame(FrameFault),
    Transport { state: u32 },
    Media { what: &'static str },
    Attach { what: &'static str },
    Message { kind: u32, what: &'static str },
}

impl fmt::Display for Fault {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match *self {
            Self::Ring {
                region,
                head,
                tail,
                capacity,
            } => write!(
                f,
                "{region} ring corrupt: head={head} tail={tail} capacity={capacity}"
            ),
            Self::Frame(fault) => write!(f, "frame record rejected: {fault}"),
            Self::Transport { state } => write!(f, "transport state {state} is not a valid state"),
            Self::Media { what } => write!(f, "media facts rejected: {what}"),
            Self::Attach { what } => write!(f, "segment rejected: {what}"),
            Self::Message { kind, what } => write!(f, "message kind {kind:#x} rejected: {what}"),
        }
    }
}

impl std::error::Error for Fault {}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum FrameFault {
    SlotOutOfRange { slot: u32 },
    SlotNotImported { slot: u32 },
    SequenceNotAdvancing { got: u64, last: u64 },
    BadDimension { value: u32 },
    VisibleExceedsPlane,
    PlaneExceedsSurface {
        plane: u32,
        claimed: u32,
        actual: u32,
    },
    BadBitDepth { bit_depth: u32 },
    BadRotation { rotation: i32 },
    NonFinitePts,
    TooFewPlanes { plane_count: u32 },
}

impl fmt::Display for FrameFault {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match *self {
            Self::SlotOutOfRange { slot } => write!(f, "surface slot {slot} out of range"),
            Self::SlotNotImported { slot } => write!(f, "surface slot {slot} not imported"),
            Self::SequenceNotAdvancing { got, last } => {
                write!(f, "sequence {got} does not advance past {last}")
            }
            Self::BadDimension { value } => write!(f, "pixel dimension {value} out of range"),
            Self::VisibleExceedsPlane => write!(f, "visible rectangle exceeds the luma plane"),
            Self::PlaneExceedsSurface {
                plane,
                claimed,
                actual,
            } => write!(
                f,
                "plane {plane} claims {claimed} px, the surface has {actual}"
            ),
            Self::BadBitDepth { bit_depth } => write!(f, "bit depth {bit_depth} unsupported"),
            Self::BadRotation { rotation } => write!(f, "rotation {rotation} is not a right angle"),
            Self::NonFinitePts => write!(f, "presentation timestamp is not finite"),
            Self::TooFewPlanes { plane_count } => {
                write!(f, "surface has {plane_count} planes, need at least 2")
            }
        }
    }
}

#[derive(Debug)]
pub enum NameError {
    TooLong { bytes: usize, limit: usize },
    Nul(NulError),
}

impl fmt::Display for NameError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::TooLong { bytes, limit } => write!(f, "name is {bytes} bytes, limit {limit}"),
            Self::Nul(error) => write!(f, "{error}"),
        }
    }
}

impl std::error::Error for NameError {}

pub fn service_name(host_pid: u32, nonce: u64) -> Result<CString, NameError> {
    CString::new(format!("{SERVICE_NAME_PREFIX}.{host_pid}.{nonce:016x}")).map_err(NameError::Nul)
}

pub fn segment_name(host_pid: u32, nonce: u64) -> Result<CString, NameError> {
    let text = format!("{SEGMENT_NAME_PREFIX}{:08x}.{:08x}", host_pid, nonce as u32);
    if text.len() > SHM_NAME_MAX {
        return Err(NameError::TooLong {
            bytes: text.len(),
            limit: SHM_NAME_MAX,
        });
    }
    CString::new(text).map_err(NameError::Nul)
}

#[cfg(target_os = "macos")]
pub fn nonce() -> u64 {
    let mut value = 0u64;
    unsafe { libc::arc4random_buf((&raw mut value).cast(), size_of::<u64>()) };
    value
}

#[cfg(windows)]
pub fn nonce() -> u64 {
    use windows_sys::Win32::Security::Cryptography::{
        BCRYPT_USE_SYSTEM_PREFERRED_RNG, BCryptGenRandom,
    };

    let mut value = 0u64;
    let status = unsafe {
        BCryptGenRandom(
            std::ptr::null_mut(),
            (&raw mut value).cast::<u8>(),
            8,
            BCRYPT_USE_SYSTEM_PREFERRED_RNG,
        )
    };
    assert!(status == 0, "BCryptGenRandom failed: {status:#010x}");
    value
}

#[cfg(all(not(target_os = "macos"), not(windows)))]
pub fn nonce() -> u64 {
    let mut value = 0u64;
    let written =
        unsafe { libc::getrandom((&raw mut value).cast::<libc::c_void>(), size_of::<u64>(), 0) };
    assert!(written == 8, "getrandom returned {written}");
    value
}

#[cfg(unix)]
pub fn uptime_nanos() -> u64 {
    #[cfg(target_os = "macos")]
    const CLOCK: libc::clockid_t = libc::CLOCK_UPTIME_RAW;
    #[cfg(not(target_os = "macos"))]
    const CLOCK: libc::clockid_t = libc::CLOCK_MONOTONIC;

    let mut now = libc::timespec {
        tv_sec: 0,
        tv_nsec: 0,
    };
    unsafe { libc::clock_gettime(CLOCK, &raw mut now) };
    (now.tv_sec as u64)
        .wrapping_mul(1_000_000_000)
        .wrapping_add(now.tv_nsec as u64)
}

#[must_use]
pub const fn ticks_to_nanos(ticks: u64, hz: u64) -> u64 {
    let (Some(seconds), Some(remainder)) = (ticks.checked_div(hz), ticks.checked_rem(hz)) else {
        return 0;
    };
    let Some(fraction) = remainder.wrapping_mul(1_000_000_000).checked_div(hz) else {
        return 0;
    };
    seconds.wrapping_mul(1_000_000_000).wrapping_add(fraction)
}

#[cfg(windows)]
pub fn uptime_nanos() -> u64 {
    use windows_sys::Win32::System::Performance::{
        QueryPerformanceCounter, QueryPerformanceFrequency,
    };

    static HZ: AtomicU64 = AtomicU64::new(0);

    let mut hz = HZ.load(Ordering::Relaxed);
    if hz == 0 {
        let mut frequency = 0i64;
        unsafe { QueryPerformanceFrequency(&raw mut frequency) };
        if frequency <= 0 {
            return 0;
        }
        hz = frequency as u64;
        HZ.store(hz, Ordering::Relaxed);
    }

    let mut count = 0i64;
    unsafe { QueryPerformanceCounter(&raw mut count) };
    ticks_to_nanos(count.max(0) as u64, hz)
}

pub mod kind {
    pub const HELLO: u32 = 0x01;
    pub const SURFACE: u32 = 0x02;
    pub const OPENED: u32 = 0x03;
    pub const FAILED: u32 = 0x04;
    pub const ENDED: u32 = 0x05;
    pub const GOODBYE: u32 = 0x06;
    pub const AUDIO: u32 = 0x07;
    pub const FETCH: u32 = 0x08;

    pub const OPEN: u32 = 0x81;
    pub const PLAY: u32 = 0x82;
    pub const PAUSE: u32 = 0x83;
    pub const CLOSE: u32 = 0x84;
    pub const SHUTDOWN: u32 = 0x85;
    pub const SET_LOG_LEVEL: u32 = 0x86;
}

pub const fn is_helper_to_host(value: u32) -> bool {
    matches!(
        value,
        kind::HELLO
            | kind::SURFACE
            | kind::OPENED
            | kind::FAILED
            | kind::ENDED
            | kind::GOODBYE
            | kind::AUDIO
            | kind::FETCH
    )
}

pub const fn is_host_to_helper(value: u32) -> bool {
    matches!(
        value,
        kind::OPEN
            | kind::PLAY
            | kind::PAUSE
            | kind::CLOSE
            | kind::SHUTDOWN
            | kind::SET_LOG_LEVEL
    )
}

pub const fn carries_right(value: u32) -> bool {
    matches!(value, kind::HELLO | kind::SURFACE)
}

pub const fn check_incoming(value: u32, has_right: bool) -> Result<(), Fault> {
    if !is_helper_to_host(value) {
        return Err(Fault::Message {
            kind: value,
            what: "not a helper-to-host kind",
        });
    }
    if has_right && !carries_right(value) {
        return Err(Fault::Message {
            kind: value,
            what: "carries a port right but must not",
        });
    }
    if !has_right && carries_right(value) {
        return Err(Fault::Message {
            kind: value,
            what: "must carry a port right but does not",
        });
    }
    Ok(())
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum HelperEvent {
    Audio,
    Surface,
    Opened,
    Failed,
    Ended,
    Goodbye,
    FetchDoorbell,
}

pub const fn classify_helper_message(value: u32) -> Result<HelperEvent, Fault> {
    match value {
        kind::AUDIO => Ok(HelperEvent::Audio),
        kind::SURFACE => Ok(HelperEvent::Surface),
        kind::OPENED => Ok(HelperEvent::Opened),
        kind::FAILED => Ok(HelperEvent::Failed),
        kind::ENDED => Ok(HelperEvent::Ended),
        kind::GOODBYE => Ok(HelperEvent::Goodbye),
        kind::FETCH => Ok(HelperEvent::FetchDoorbell),
        _ => Err(Fault::Message {
            kind: value,
            what: "not valid after the handshake",
        }),
    }
}

pub mod error {
    pub const OPEN_FAILED: u64 = 1;
    pub const DECODE_FAILED: u64 = 4;
}

pub mod fetch_op {
    pub const OPEN: u32 = 1;
    pub const READ: u32 = 2;
    pub const CLOSE: u32 = 3;
}

pub mod fetch_status {
    pub const OK: u32 = 0;
    pub const EOF: u32 = 1;
    pub const ERR: u32 = 2;
}

#[repr(C, align(128))]
pub struct Seqlock<const WORDS: usize> {
    sequence: AtomicU32,
    reserved: AtomicU32,
    words: [AtomicU64; WORDS],
}

impl<const WORDS: usize> Seqlock<WORDS> {
    pub fn write(&self, words: &[u64; WORDS]) {
        let start = self.sequence.load(Ordering::Relaxed);
        self.sequence.store(start.wrapping_add(1), Ordering::Relaxed);
        fence(Ordering::Release);
        for (cell, value) in self.words.iter().zip(words.iter()) {
            cell.store(*value, Ordering::Relaxed);
        }
        fence(Ordering::Release);
        self.sequence.store(start.wrapping_add(2), Ordering::Relaxed);
    }

    pub fn read(&self) -> Option<[u64; WORDS]> {
        let mut attempts = SEQLOCK_READ_ATTEMPTS;
        while attempts > 0 {
            attempts = attempts.wrapping_sub(1);
            let before = self.sequence.load(Ordering::Acquire);
            if before & 1 != 0 {
                continue;
            }
            let mut out = [0u64; WORDS];
            for (slot, cell) in out.iter_mut().zip(self.words.iter()) {
                *slot = cell.load(Ordering::Relaxed);
            }
            fence(Ordering::Acquire);
            if self.sequence.load(Ordering::Relaxed) == before {
                return Some(out);
            }
        }
        None
    }

    pub fn is_published(&self) -> bool {
        self.sequence.load(Ordering::Acquire) != 0
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ClockWire {
    pub base: f64,
    pub anchor_nanos: u64,
    pub rate: f64,
}

impl ClockWire {
    pub const HELD_AT_ZERO: Self = Self {
        base: 0.0,
        anchor_nanos: 0,
        rate: 1.0,
    };

    pub fn now(&self, now_nanos: u64) -> f64 {
        if self.anchor_nanos == 0 {
            return self.base;
        }
        let elapsed = now_nanos.saturating_sub(self.anchor_nanos);
        ((elapsed as f64) * 1e-9).mul_add(self.rate, self.base)
    }

    fn sanitised(self) -> Self {
        Self {
            base: if self.base.is_finite() { self.base } else { 0.0 },
            anchor_nanos: self.anchor_nanos,
            rate: if self.rate.is_finite() && self.rate > 0.0 && self.rate <= 16.0 {
                self.rate
            } else {
                1.0
            },
        }
    }
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PlaybackState {
    Ready = 2,
    Playing = 3,
    Paused = 4,
    Ended = 5,
}

impl PlaybackState {
    const fn from_wire(value: u32) -> Option<Self> {
        match value {
            2 => Some(Self::Ready),
            3 => Some(Self::Playing),
            4 => Some(Self::Paused),
            5 => Some(Self::Ended),
            _ => None,
        }
    }

    pub const fn to_wire(self) -> u32 {
        self as u32
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct TransportSnapshot {
    pub state: PlaybackState,
    pub clock: ClockWire,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum TransportRead {
    Fresh(TransportSnapshot),
    Contended,
    Corrupt(Fault),
}

#[repr(C)]
pub struct Transport(Seqlock<4>);

impl Transport {
    pub fn publish(&self, snapshot: TransportSnapshot) {
        self.0.write(&[
            u64::from(snapshot.state.to_wire()),
            snapshot.clock.base.to_bits(),
            snapshot.clock.anchor_nanos,
            snapshot.clock.rate.to_bits(),
        ]);
    }

    pub fn read(&self) -> TransportRead {
        let Some(words) = self.0.read() else {
            return TransportRead::Contended;
        };
        let [state_word, base, anchor, rate] = words;
        let Ok(narrowed) = u32::try_from(state_word) else {
            return TransportRead::Corrupt(Fault::Transport { state: u32::MAX });
        };
        let Some(state) = PlaybackState::from_wire(narrowed) else {
            return TransportRead::Corrupt(Fault::Transport { state: narrowed });
        };
        TransportRead::Fresh(TransportSnapshot {
            state,
            clock: ClockWire {
                base: f64::from_bits(base),
                anchor_nanos: anchor,
                rate: f64::from_bits(rate),
            }
            .sanitised(),
        })
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct MediaFactsValue {
    pub open_generation: u64,
    pub duration: f64,
    pub visible_width: u32,
    pub visible_height: u32,
    pub has_video: bool,
    pub has_audio: bool,
    pub sample_rate: u32,
    pub channels: u32,
}

#[repr(C)]
pub struct MediaFacts(Seqlock<5>);

impl MediaFacts {
    pub fn publish(&self, facts: MediaFactsValue) {
        let dims = (u64::from(facts.visible_width) << 32) | u64::from(facts.visible_height);
        let audio = (u64::from(facts.sample_rate) << 32) | u64::from(facts.channels);
        let flags = u64::from(facts.has_video) | (u64::from(facts.has_audio) << 1);
        self.0
            .write(&[facts.open_generation, facts.duration.to_bits(), dims, audio, flags]);
    }

    pub fn read(&self) -> Result<Option<MediaFactsValue>, Fault> {
        if !self.0.is_published() {
            return Ok(None);
        }
        let Some(words) = self.0.read() else {
            return Ok(None);
        };
        let [open_generation, duration_bits, dims, audio, flags] = words;

        let duration = f64::from_bits(duration_bits);
        let duration = if duration.is_finite() && duration >= 0.0 {
            duration
        } else {
            0.0
        };
        let has_video = flags & 1 != 0;
        let visible_width = (dims >> 32) as u32;
        let visible_height = (dims & 0xffff_ffff) as u32;
        if has_video && !in_dimension_range(visible_width) {
            return Err(Fault::Media {
                what: "visible width out of range",
            });
        }
        if has_video && !in_dimension_range(visible_height) {
            return Err(Fault::Media {
                what: "visible height out of range",
            });
        }

        let has_audio = flags & 2 != 0;
        let sample_rate = (audio >> 32) as u32;
        let channels = (audio & 0xffff_ffff) as u32;
        if has_audio && !(1..=768_000).contains(&sample_rate) {
            return Err(Fault::Media {
                what: "sample rate out of range",
            });
        }
        if has_audio && !(1..=64).contains(&channels) {
            return Err(Fault::Media {
                what: "channel count out of range",
            });
        }

        Ok(Some(MediaFactsValue {
            open_generation,
            duration,
            visible_width,
            visible_height,
            has_video,
            has_audio,
            sample_rate,
            channels,
        }))
    }
}

const fn in_dimension_range(value: u32) -> bool {
    value >= 1 && value <= MAX_PLANE_DIMENSION
}

#[repr(C, align(128))]
pub struct CancelFlag(AtomicU32);

impl CancelFlag {
    pub fn set(&self) {
        self.0.store(1, Ordering::Release);
    }

    pub fn clear(&self) {
        self.0.store(0, Ordering::Release);
    }

    pub fn is_set(&self) -> bool {
        self.0.load(Ordering::Acquire) != 0
    }

    pub const fn as_ptr(&self) -> *const AtomicU32 {
        &raw const self.0
    }
}

#[repr(C, align(128))]
pub struct SeekSlot {
    request: AtomicU64,
    serviced: AtomicU64,
    target: AtomicU64,
}

impl SeekSlot {
    pub fn request(&self, target: f64) {
        let target = if target.is_finite() && target >= 0.0 {
            target
        } else {
            0.0
        };
        self.target.store(target.to_bits(), Ordering::Relaxed);
        let next = self.request.load(Ordering::Relaxed).wrapping_add(1);
        self.request.store(next, Ordering::Release);
    }

    pub fn is_pending(&self) -> bool {
        self.request.load(Ordering::Acquire) != self.serviced.load(Ordering::Relaxed)
    }

    pub fn take(&self) -> Option<f64> {
        let request = self.request.load(Ordering::Acquire);
        if request == self.serviced.load(Ordering::Relaxed) {
            return None;
        }
        let target = f64::from_bits(self.target.load(Ordering::Relaxed));
        self.serviced.store(request, Ordering::Release);
        Some(if target.is_finite() && target >= 0.0 {
            target
        } else {
            0.0
        })
    }
}

#[repr(C, align(128))]
pub struct OpenSlot {
    generation: AtomicU64,
    url_len: AtomicU32,
    reserved: AtomicU32,
    url: [AtomicU8; URL_MAX_BYTES],
}

impl OpenSlot {
    pub fn publish(&self, url: &str) -> Option<u64> {
        let bytes = url.as_bytes();
        if bytes.len() > URL_MAX_BYTES {
            return None;
        }
        for (cell, byte) in self.url.iter().zip(bytes.iter()) {
            cell.store(*byte, Ordering::Relaxed);
        }
        self.url_len.store(bytes.len() as u32, Ordering::Relaxed);
        let next = self.generation.load(Ordering::Relaxed).wrapping_add(1);
        self.generation.store(next, Ordering::Release);
        Some(next)
    }

    pub fn take(&self, generation: u64) -> Option<String> {
        if self.generation.load(Ordering::Acquire) != generation {
            return None;
        }
        let len = (self.url_len.load(Ordering::Relaxed) as usize).min(URL_MAX_BYTES);
        let mut bytes = Vec::with_capacity(len);
        for cell in self.url.iter().take(len) {
            bytes.push(cell.load(Ordering::Relaxed));
        }
        if self.generation.load(Ordering::Acquire) != generation {
            return None;
        }
        String::from_utf8(bytes).ok()
    }
}

#[repr(C, align(128))]
pub struct AudioOptionsCell {
    packed: AtomicU64,
    generation: AtomicU64,
}

impl AudioOptionsCell {
    pub fn publish(&self, sample_rate: u32, channels: u32) {
        self.packed.store(
            (u64::from(sample_rate) << 32) | u64::from(channels),
            Ordering::Relaxed,
        );
        let next = self.generation.load(Ordering::Relaxed).wrapping_add(1);
        self.generation.store(next, Ordering::Release);
    }

    pub fn read(&self) -> Option<(u32, u32, u64)> {
        let generation = self.generation.load(Ordering::Acquire);
        if generation == 0 {
            return None;
        }
        let packed = self.packed.load(Ordering::Relaxed);
        Some(((packed >> 32) as u32, (packed & 0xffff_ffff) as u32, generation))
    }
}

#[repr(C)]
pub struct AudioFormatCell(Seqlock<3>);

impl AudioFormatCell {
    pub fn publish(
        &self,
        sample_rate: u32,
        channels: u32,
        options_generation: u64,
        ring_generation: u64,
    ) {
        self.0.write(&[
            (u64::from(sample_rate) << 32) | u64::from(channels),
            options_generation,
            ring_generation,
        ]);
    }

    pub fn read(&self) -> Option<(u32, u32, u64, u64)> {
        if !self.0.is_published() {
            return None;
        }
        let [packed, options_generation, ring_generation] = self.0.read()?;
        Some((
            (packed >> 32) as u32,
            (packed & 0xffff_ffff) as u32,
            options_generation,
            ring_generation,
        ))
    }
}

fn sanitise_rate(rate: f64) -> f64 {
    if rate.is_finite() && rate > 0.0 && rate <= 16.0 {
        rate
    } else {
        1.0
    }
}

#[repr(C, align(128))]
pub struct ControlsCell {
    looping: AtomicU32,
    reserved: AtomicU32,
    rate_bits: AtomicU64,
    rate_generation: AtomicU64,
    master_bits: AtomicU64,
    master_generation: AtomicU64,
}

impl ControlsCell {
    pub fn set_looping(&self, looping: bool) {
        self.looping.store(u32::from(looping), Ordering::Release);
    }

    pub fn looping(&self) -> bool {
        self.looping.load(Ordering::Acquire) != 0
    }

    pub fn request_rate(&self, rate: f64) -> u64 {
        let rate = sanitise_rate(rate);
        self.rate_bits.store(rate.to_bits(), Ordering::Relaxed);
        let next = self.rate_generation.load(Ordering::Relaxed).wrapping_add(1);
        self.rate_generation.store(next, Ordering::Release);
        next
    }

    pub fn requested_rate(&self) -> (f64, u64) {
        let generation = self.rate_generation.load(Ordering::Acquire);
        let rate = f64::from_bits(self.rate_bits.load(Ordering::Relaxed));
        (sanitise_rate(rate), generation)
    }

    pub fn request_master_clock(&self, seconds: f64) -> u64 {
        let seconds = if seconds.is_finite() { seconds } else { 0.0 };
        self.master_bits.store(seconds.to_bits(), Ordering::Relaxed);
        let next = self
            .master_generation
            .load(Ordering::Relaxed)
            .wrapping_add(1);
        self.master_generation.store(next, Ordering::Release);
        next
    }

    pub fn master_clock(&self) -> Option<(f64, u64)> {
        let generation = self.master_generation.load(Ordering::Acquire);
        if generation == 0 {
            return None;
        }
        let seconds = f64::from_bits(self.master_bits.load(Ordering::Relaxed));
        Some((if seconds.is_finite() { seconds } else { 0.0 }, generation))
    }
}

#[repr(C, align(128))]
pub struct ControlsEcho {
    applied_looping: AtomicU32,
    reserved: AtomicU32,
    applied_rate_generation: AtomicU64,
}

impl ControlsEcho {
    pub fn publish(&self, applied_looping: bool, applied_rate_generation: u64) {
        self.applied_looping
            .store(u32::from(applied_looping), Ordering::Relaxed);
        self.applied_rate_generation
            .store(applied_rate_generation, Ordering::Release);
    }

    pub fn read(&self) -> (bool, u64) {
        let generation = self.applied_rate_generation.load(Ordering::Acquire);
        (self.applied_looping.load(Ordering::Relaxed) != 0, generation)
    }
}

pub const PROTOCOL_WHITELIST_MAX_BYTES: usize = 256;

#[repr(C, align(128))]
pub struct ProtocolWhitelistCell {
    len: AtomicU32,
    reserved: AtomicU32,
    text: [AtomicU8; PROTOCOL_WHITELIST_MAX_BYTES],
}

impl ProtocolWhitelistCell {
    pub fn publish(&self, whitelist: &str) -> bool {
        let bytes = whitelist.as_bytes();
        if bytes.is_empty() || bytes.len() > PROTOCOL_WHITELIST_MAX_BYTES {
            return false;
        }
        for (cell, byte) in self.text.iter().zip(bytes.iter()) {
            cell.store(*byte, Ordering::Relaxed);
        }
        self.len.store(bytes.len() as u32, Ordering::Release);
        true
    }

    pub fn read(&self) -> Option<String> {
        let len =
            (self.len.load(Ordering::Acquire) as usize).min(PROTOCOL_WHITELIST_MAX_BYTES);
        if len == 0 {
            return None;
        }
        let mut bytes = Vec::with_capacity(len);
        for cell in self.text.iter().take(len) {
            bytes.push(cell.load(Ordering::Relaxed));
        }
        let text = String::from_utf8(bytes).ok()?;
        if text.bytes().all(|b| b.is_ascii_alphanumeric() || b == b',') {
            Some(text)
        } else {
            None
        }
    }
}

#[repr(C, align(128))]
pub struct AdapterCell {
    luid: AtomicU64,
    published: AtomicU32,
    reserved: AtomicU32,
}

impl AdapterCell {
    pub fn publish(&self, luid: u64) {
        self.luid.store(luid, Ordering::Relaxed);
        self.published.store(1, Ordering::Release);
    }

    pub fn read(&self) -> Option<u64> {
        if self.published.load(Ordering::Acquire) == 0 {
            return None;
        }
        let luid = self.luid.load(Ordering::Relaxed);
        (luid != 0).then_some(luid)
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct FetchRequest {
    pub generation: u64,
    pub op: u32,
    pub handle: u32,
    pub offset: u64,
    pub len: u32,
    pub flags: u32,
    pub url: String,
}

#[repr(C, align(128))]
pub struct FetchRequestCell {
    generation: AtomicU64,
    op: AtomicU32,
    handle: AtomicU32,
    offset: AtomicU64,
    len: AtomicU32,
    flags: AtomicU32,
    url_len: AtomicU32,
    sequence: AtomicU32,
    url: [AtomicU8; URL_MAX_BYTES],
}

impl FetchRequestCell {
    pub fn publish(&self, op: u32, handle: u32, offset: u64, len: u32, flags: u32, url: &str) -> Option<u64> {
        let bytes = url.as_bytes();
        if bytes.len() > URL_MAX_BYTES {
            return None;
        }
        let start = self.sequence.load(Ordering::Relaxed);
        self.sequence.store(start.wrapping_add(1), Ordering::Relaxed);
        fence(Ordering::Release);
        for (cell, byte) in self.url.iter().zip(bytes.iter()) {
            cell.store(*byte, Ordering::Relaxed);
        }
        self.url_len.store(bytes.len() as u32, Ordering::Relaxed);
        self.op.store(op, Ordering::Relaxed);
        self.handle.store(handle, Ordering::Relaxed);
        self.offset.store(offset, Ordering::Relaxed);
        self.len.store(len, Ordering::Relaxed);
        self.flags.store(flags, Ordering::Relaxed);
        let next = self.generation.load(Ordering::Relaxed).wrapping_add(1);
        self.generation.store(next, Ordering::Relaxed);
        fence(Ordering::Release);
        self.sequence.store(start.wrapping_add(2), Ordering::Release);
        Some(next)
    }

    pub fn take(&self, last_seen: u64) -> Option<FetchRequest> {
        let sequence = self.sequence.load(Ordering::Acquire);
        if sequence & 1 != 0 {
            return None;
        }
        let generation = self.generation.load(Ordering::Relaxed);
        if generation == last_seen {
            return None;
        }
        let op = self.op.load(Ordering::Relaxed);
        let handle = self.handle.load(Ordering::Relaxed);
        let offset = self.offset.load(Ordering::Relaxed);
        let len = self.len.load(Ordering::Relaxed).min(FETCH_SLOT_BYTES as u32);
        let flags = self.flags.load(Ordering::Relaxed);
        let url_len = (self.url_len.load(Ordering::Relaxed) as usize).min(URL_MAX_BYTES);
        let mut bytes = Vec::with_capacity(url_len);
        for cell in self.url.iter().take(url_len) {
            bytes.push(cell.load(Ordering::Relaxed));
        }
        fence(Ordering::Acquire);
        if self.sequence.load(Ordering::Relaxed) != sequence {
            return None;
        }
        Some(FetchRequest {
            generation,
            op,
            handle,
            offset,
            len,
            flags,
            url: String::from_utf8_lossy(&bytes).into_owned(),
        })
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct FetchResponse {
    pub status: u32,
    pub n: u32,
    pub size: i64,
    pub out_handle: u32,
}

#[repr(C, align(128))]
pub struct FetchResponseCell {
    response: AtomicU64,
    status: AtomicU32,
    n: AtomicU32,
    size: AtomicI64,
    out_handle: AtomicU32,
    reserved: AtomicU32,
}

impl FetchResponseCell {
    pub fn publish(&self, generation: u64, status: u32, n: u32, size: i64, out_handle: u32) {
        self.status.store(status, Ordering::Relaxed);
        self.n.store(n.min(FETCH_SLOT_BYTES as u32), Ordering::Relaxed);
        self.size.store(size, Ordering::Relaxed);
        self.out_handle.store(out_handle, Ordering::Relaxed);
        self.response.store(generation, Ordering::Release);
    }

    pub fn read(&self, generation: u64) -> Option<FetchResponse> {
        if self.response.load(Ordering::Acquire) != generation {
            return None;
        }
        Some(FetchResponse {
            status: self.status.load(Ordering::Relaxed),
            n: self.n.load(Ordering::Relaxed).min(FETCH_SLOT_BYTES as u32),
            size: self.size.load(Ordering::Relaxed),
            out_handle: self.out_handle.load(Ordering::Relaxed),
        })
    }
}

#[repr(C, align(128))]
pub struct FetchBulk {
    bytes: [AtomicU8; FETCH_SLOT_BYTES],
}

impl FetchBulk {
    pub fn stage(&self, src: &[u8]) -> u32 {
        let n = src.len().min(FETCH_SLOT_BYTES);
        for (cell, byte) in self.bytes.iter().zip(src.iter().take(n)) {
            cell.store(*byte, Ordering::Relaxed);
        }
        n as u32
    }

    pub fn copy_out(&self, n: usize, dst: &mut [u8]) -> usize {
        let n = n.min(FETCH_SLOT_BYTES).min(dst.len());
        for (byte, cell) in dst.iter_mut().zip(self.bytes.iter()).take(n) {
            *byte = cell.load(Ordering::Relaxed);
        }
        n
    }
}

#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct FrameInfoWire {
    pub yuv_to_rgb: [f32; 12],
    pub uv_transform: [f32; 6],
    pub visible_width: u32,
    pub visible_height: u32,
    pub plane_width: [u32; 2],
    pub plane_height: [u32; 2],
    pub colorspace: i32,
    pub color_range: i32,
    pub color_primaries: i32,
    pub rotation: i32,
    pub bit_depth: u32,
}

pub const FRAME_FLAG_HAS_PTS: u32 = 1 << 0;

#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct FrameRecord {
    pub info: FrameInfoWire,
    pub flags: u32,
    pub pts: f64,
    pub sequence: u64,
    pub slot: u32,
    pub reserved: u32,
}

pub const FRAME_INFO_WIRE_BYTES: usize = 116;
pub const FRAME_RECORD_BYTES: usize = 144;
pub const FRAME_RECORD_WORDS: usize = FRAME_RECORD_BYTES / 4;

const _: () = assert!(size_of::<FrameInfoWire>() == FRAME_INFO_WIRE_BYTES);
const _: () = assert!(align_of::<FrameInfoWire>() == 4);
const _: () = assert!(size_of::<FrameRecord>() == FRAME_RECORD_BYTES);
const _: () = assert!(align_of::<FrameRecord>() == 8);
const _: () = assert!(std::mem::offset_of!(FrameRecord, flags) == 116);
const _: () = assert!(std::mem::offset_of!(FrameRecord, pts) == 120);
const _: () = assert!(std::mem::offset_of!(FrameRecord, sequence) == 128);
const _: () = assert!(std::mem::offset_of!(FrameRecord, slot) == 136);
const _: () = assert!(std::mem::offset_of!(FrameRecord, reserved) == 140);

pub const FRAME_INFO_BYTES: usize = 152;

const _: () = assert!(size_of::<uuav_abi::FrameInfo>() == FRAME_INFO_BYTES);

const _: () = assert!(std::mem::offset_of!(FrameInfoWire, yuv_to_rgb) == 0);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, uv_transform) == 48);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, visible_width) == 72);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, visible_height) == 76);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, plane_width) == 80);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, plane_height) == 88);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, colorspace) == 96);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, color_range) == 100);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, color_primaries) == 104);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, rotation) == 108);
const _: () = assert!(std::mem::offset_of!(FrameInfoWire, bit_depth) == 112);

const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, yuv_to_rgb)
        == std::mem::offset_of!(FrameInfoWire, yuv_to_rgb)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, uv_transform)
        == std::mem::offset_of!(FrameInfoWire, uv_transform)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, visible_width)
        == std::mem::offset_of!(FrameInfoWire, visible_width)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, visible_height)
        == std::mem::offset_of!(FrameInfoWire, visible_height)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, plane_width)
        == std::mem::offset_of!(FrameInfoWire, plane_width)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, plane_height)
        == std::mem::offset_of!(FrameInfoWire, plane_height)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, colorspace)
        == std::mem::offset_of!(FrameInfoWire, colorspace)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, color_range)
        == std::mem::offset_of!(FrameInfoWire, color_range)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, color_primaries)
        == std::mem::offset_of!(FrameInfoWire, color_primaries)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, rotation)
        == std::mem::offset_of!(FrameInfoWire, rotation)
);
const _: () = assert!(
    std::mem::offset_of!(uuav_abi::FrameInfo, bit_depth)
        == std::mem::offset_of!(FrameInfoWire, bit_depth)
);

impl FrameRecord {
    fn to_words(self) -> [u32; FRAME_RECORD_WORDS] {
        unsafe { std::mem::transmute::<Self, [u32; FRAME_RECORD_WORDS]>(self) }
    }

    fn from_words(words: [u32; FRAME_RECORD_WORDS]) -> Self {
        unsafe { std::mem::transmute::<[u32; FRAME_RECORD_WORDS], Self>(words) }
    }

    pub fn validate(
        &self,
        last_sequence: u64,
        surface: &SurfaceGeometry,
    ) -> Result<ValidFrame, FrameFault> {
        let slot = self.slot as usize;
        if slot >= SURFACE_SLOT_COUNT {
            return Err(FrameFault::SlotOutOfRange { slot: self.slot });
        }
        if self.sequence <= last_sequence {
            return Err(FrameFault::SequenceNotAdvancing {
                got: self.sequence,
                last: last_sequence,
            });
        }

        if surface.plane_count < 2 {
            return Err(FrameFault::TooFewPlanes {
                plane_count: surface.plane_count,
            });
        }

        let info = self.info;
        for value in [info.visible_width, info.visible_height] {
            if !in_dimension_range(value) {
                return Err(FrameFault::BadDimension { value });
            }
        }
        for plane in 0..2u32 {
            let index = plane as usize;
            let (Some(&claimed_w), Some(&claimed_h)) =
                (info.plane_width.get(index), info.plane_height.get(index))
            else {
                continue;
            };
            let (Some(&actual_w), Some(&actual_h)) = (
                surface.plane_width.get(index),
                surface.plane_height.get(index),
            ) else {
                continue;
            };
            if !in_dimension_range(claimed_w) {
                return Err(FrameFault::BadDimension { value: claimed_w });
            }
            if !in_dimension_range(claimed_h) {
                return Err(FrameFault::BadDimension { value: claimed_h });
            }
            if claimed_w > actual_w {
                return Err(FrameFault::PlaneExceedsSurface {
                    plane,
                    claimed: claimed_w,
                    actual: actual_w,
                });
            }
            if claimed_h > actual_h {
                return Err(FrameFault::PlaneExceedsSurface {
                    plane,
                    claimed: claimed_h,
                    actual: actual_h,
                });
            }
        }
        let (Some(&luma_w), Some(&luma_h)) = (info.plane_width.first(), info.plane_height.first())
        else {
            return Err(FrameFault::VisibleExceedsPlane);
        };
        if info.visible_width > luma_w || info.visible_height > luma_h {
            return Err(FrameFault::VisibleExceedsPlane);
        }

        if info.bit_depth != 8 && info.bit_depth != 10 {
            return Err(FrameFault::BadBitDepth {
                bit_depth: info.bit_depth,
            });
        }
        if !matches!(info.rotation, 0 | 90 | 180 | 270) {
            return Err(FrameFault::BadRotation {
                rotation: info.rotation,
            });
        }

        let has_pts = self.flags & FRAME_FLAG_HAS_PTS != 0;
        if has_pts && !self.pts.is_finite() {
            return Err(FrameFault::NonFinitePts);
        }

        Ok(ValidFrame {
            sequence: self.sequence,
            slot,
            pts: has_pts.then_some(self.pts),
            info: sanitise_info(info),
        })
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct ValidFrame {
    pub sequence: u64,
    pub slot: usize,
    pub pts: Option<f64>,
    pub info: FrameInfoWire,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SurfaceGeometry {
    pub plane_width: [u32; 2],
    pub plane_height: [u32; 2],
    pub plane_count: u32,
}

pub const fn assemble(
    frame: &ValidFrame,
    frame_index: u64,
    surface_generation: u64,
    planes: [usize; 2],
) -> uuav_abi::FrameInfo {
    let wire = frame.info;
    uuav_abi::FrameInfo {
        yuv_to_rgb: wire.yuv_to_rgb,
        uv_transform: wire.uv_transform,
        visible_width: wire.visible_width,
        visible_height: wire.visible_height,
        plane_width: wire.plane_width,
        plane_height: wire.plane_height,
        colorspace: wire.colorspace,
        color_range: wire.color_range,
        color_primaries: wire.color_primaries,
        rotation: wire.rotation,
        bit_depth: wire.bit_depth,
        frame_index,
        surface_generation,
        planes,
    }
}

const MAX_SHADER_CONSTANT: f32 = 16.0;

const AVCOL_SPC_UNSPECIFIED: i32 = 2;
const AVCOL_RANGE_UNSPECIFIED: i32 = 0;
const AVCOL_PRI_UNSPECIFIED: i32 = 2;

fn sanitise_info(mut info: FrameInfoWire) -> FrameInfoWire {
    for value in &mut info.yuv_to_rgb {
        *value = finite_clamped(*value);
    }
    for value in &mut info.uv_transform {
        *value = finite_clamped(*value);
    }
    if !(0..=22).contains(&info.colorspace) {
        info.colorspace = AVCOL_SPC_UNSPECIFIED;
    }
    if !(0..=2).contains(&info.color_range) {
        info.color_range = AVCOL_RANGE_UNSPECIFIED;
    }
    if !(0..=22).contains(&info.color_primaries) {
        info.color_primaries = AVCOL_PRI_UNSPECIFIED;
    }
    info
}

fn finite_clamped(value: f32) -> f32 {
    if value.is_finite() {
        value.clamp(-MAX_SHADER_CONSTANT, MAX_SHADER_CONSTANT)
    } else {
        0.0
    }
}

#[repr(C, align(128))]
struct Cursor(AtomicU64);

impl Cursor {
    fn load(&self, ordering: Ordering) -> u64 {
        self.0.load(ordering)
    }

    fn store(&self, value: u64, ordering: Ordering) {
        self.0.store(value, ordering);
    }
}

const fn filled(region: &'static str, head: u64, tail: u64, capacity: usize) -> Result<u64, Fault> {
    let filled = head.wrapping_sub(tail);
    if filled > capacity as u64 {
        return Err(Fault::Ring {
            region,
            head,
            tail,
            capacity: capacity as u64,
        });
    }
    Ok(filled)
}

#[repr(C)]
pub struct FrameSlot {
    words: [AtomicU32; FRAME_RECORD_WORDS],
}

impl FrameSlot {
    fn store(&self, record: &FrameRecord) {
        for (cell, value) in self.words.iter().zip(record.to_words().iter()) {
            cell.store(*value, Ordering::Relaxed);
        }
    }

    fn load(&self) -> FrameRecord {
        let mut words = [0u32; FRAME_RECORD_WORDS];
        for (slot, cell) in words.iter_mut().zip(self.words.iter()) {
            *slot = cell.load(Ordering::Relaxed);
        }
        FrameRecord::from_words(words)
    }
}

#[repr(C)]
pub struct VideoRing {
    head: Cursor,
    tail: Cursor,
    slots: [FrameSlot; VIDEO_RING_CAPACITY],
}

impl VideoRing {
    pub fn publish(&self, record: &FrameRecord) -> bool {
        let head = self.head.load(Ordering::Relaxed);
        let tail = self.tail.load(Ordering::Acquire);
        if head.wrapping_sub(tail) >= VIDEO_RING_CAPACITY as u64 {
            return false;
        }
        let Some(slot) = self.slots.get(ring_slot(head, VIDEO_RING_CAPACITY)) else {
            return false;
        };
        slot.store(record);
        self.head.store(head.wrapping_add(1), Ordering::Release);
        true
    }

    pub fn peek(&self) -> Result<Option<FrameRecord>, Fault> {
        let tail = self.tail.load(Ordering::Relaxed);
        let head = self.head.load(Ordering::Acquire);
        if filled("video", head, tail, VIDEO_RING_CAPACITY)? == 0 {
            return Ok(None);
        }
        let Some(slot) = self.slots.get(ring_slot(tail, VIDEO_RING_CAPACITY)) else {
            return Err(Fault::Ring {
                region: "video",
                head,
                tail,
                capacity: VIDEO_RING_CAPACITY as u64,
            });
        };
        Ok(Some(slot.load()))
    }

    pub fn commit(&self) {
        let tail = self.tail.load(Ordering::Relaxed);
        self.tail.store(tail.wrapping_add(1), Ordering::Release);
    }

    pub fn depth(&self) -> Result<u64, Fault> {
        let tail = self.tail.load(Ordering::Acquire);
        let head = self.head.load(Ordering::Acquire);
        filled("video", head, tail, VIDEO_RING_CAPACITY)
    }
}

#[repr(C)]
pub struct ReleaseRing {
    head: Cursor,
    tail: Cursor,
    slots: [AtomicU64; RELEASE_RING_CAPACITY],
}

impl ReleaseRing {
    pub fn release(&self, sequence: u64) -> bool {
        let head = self.head.load(Ordering::Relaxed);
        let tail = self.tail.load(Ordering::Acquire);
        if head.wrapping_sub(tail) >= RELEASE_RING_CAPACITY as u64 {
            return false;
        }
        let Some(cell) = self.slots.get(ring_slot(head, RELEASE_RING_CAPACITY)) else {
            return false;
        };
        cell.store(sequence, Ordering::Relaxed);
        self.head.store(head.wrapping_add(1), Ordering::Release);
        true
    }

    pub fn take(&self) -> Result<Option<u64>, Fault> {
        let tail = self.tail.load(Ordering::Relaxed);
        let head = self.head.load(Ordering::Acquire);
        if filled("release", head, tail, RELEASE_RING_CAPACITY)? == 0 {
            return Ok(None);
        }
        let Some(cell) = self.slots.get(ring_slot(tail, RELEASE_RING_CAPACITY)) else {
            return Err(Fault::Ring {
                region: "release",
                head,
                tail,
                capacity: RELEASE_RING_CAPACITY as u64,
            });
        };
        let sequence = cell.load(Ordering::Relaxed);
        self.tail.store(tail.wrapping_add(1), Ordering::Release);
        Ok(Some(sequence))
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct VerifyEntry {
    pub sequence: u64,
    pub checksum: u64,
    pub byte_count: u64,
    pub decode_ready_nanos: u64,
    pub published_nanos: u64,
}

#[repr(C)]
struct VerifySlot {
    sequence: AtomicU64,
    checksum: AtomicU64,
    byte_count: AtomicU64,
    decode_ready_nanos: AtomicU64,
    published_nanos: AtomicU64,
}

pub const VERIFY_RING_CAPACITY: usize = VIDEO_RING_CAPACITY * 2;

#[repr(C)]
pub struct VerifyRing {
    slots: [VerifySlot; VERIFY_RING_CAPACITY],
}

impl VerifyRing {
    pub fn publish(&self, entry: VerifyEntry) {
        let Some(slot) = self.slots.get(ring_slot(entry.sequence, VERIFY_RING_CAPACITY)) else {
            return;
        };
        slot.sequence.store(0, Ordering::Relaxed);
        fence(Ordering::Release);
        slot.checksum.store(entry.checksum, Ordering::Relaxed);
        slot.byte_count.store(entry.byte_count, Ordering::Relaxed);
        slot.decode_ready_nanos
            .store(entry.decode_ready_nanos, Ordering::Relaxed);
        slot.published_nanos
            .store(entry.published_nanos, Ordering::Relaxed);
        fence(Ordering::Release);
        slot.sequence.store(entry.sequence, Ordering::Release);
    }

    pub fn lookup(&self, sequence: u64) -> Option<VerifyEntry> {
        let slot = self.slots.get(ring_slot(sequence, VERIFY_RING_CAPACITY))?;
        if slot.sequence.load(Ordering::Acquire) != sequence {
            return None;
        }
        let checksum = slot.checksum.load(Ordering::Relaxed);
        let byte_count = slot.byte_count.load(Ordering::Relaxed);
        let decode_ready_nanos = slot.decode_ready_nanos.load(Ordering::Relaxed);
        let published_nanos = slot.published_nanos.load(Ordering::Relaxed);
        fence(Ordering::Acquire);
        if slot.sequence.load(Ordering::Relaxed) != sequence {
            return None;
        }
        Some(VerifyEntry {
            sequence,
            checksum,
            byte_count,
            decode_ready_nanos,
            published_nanos,
        })
    }
}

pub fn checksum_luma(plane: &[u8], stride: usize, width: usize, height: usize) -> (u64, u64) {
    let mut total = 0u32;
    let mut covered = 0u64;
    for row in 0..height {
        let Some(start) = row.checked_mul(stride) else {
            break;
        };
        let Some(end) = start.checked_add(width) else {
            break;
        };
        let Some(bytes) = plane.get(start..end) else {
            break;
        };
        let mut hash = 0x811c_9dc5_u32;
        for byte in bytes {
            hash ^= u32::from(*byte);
            hash = hash.wrapping_mul(0x0100_0193);
        }
        total = total.wrapping_add(hash);
        covered = covered.wrapping_add(width as u64);
    }
    (u64::from(total), covered)
}

pub const LUMA_CHECKSUM_MSL: &str = r"
#include <metal_stdlib>
using namespace metal;

kernel void uuav_luma_fnv1a(texture2d<uint, access::read> luma  [[texture(0)]],
                            device atomic_uint            *total [[buffer(0)]],
                            constant uint2                &extent [[buffer(1)]],
                            uint row [[thread_position_in_grid]])
{
    if (row >= extent.y) { return; }
    uint hash = 0x811c9dc5u;
    for (uint x = 0; x < extent.x; ++x) {
        hash ^= (luma.read(uint2(x, row)).r & 0xffu);
        hash *= 0x01000193u;
    }
    atomic_fetch_add_explicit(total, hash, memory_order_relaxed);
}
";

pub const LUMA_CHECKSUM_FUNCTION: &str = "uuav_luma_fnv1a";

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct AudioMarker {
    pub position: u64,
    pub pts: f64,
}

#[repr(C)]
struct AudioMarkerSlot {
    position: AtomicU64,
    pts: AtomicU64,
}

#[repr(C)]
pub struct AudioRing {
    head: Cursor,
    tail: Cursor,
    marker_head: Cursor,
    marker_tail: Cursor,
    generation: Cursor,
    samples: [AtomicU32; AUDIO_SAMPLE_CAPACITY],
    markers: [AudioMarkerSlot; AUDIO_MARKER_CAPACITY],
}

impl AudioRing {
    pub fn generation(&self) -> u64 {
        self.generation.load(Ordering::Acquire)
    }

    pub fn restart(&self) -> u64 {
        let head = self.head.load(Ordering::Relaxed);
        self.tail.store(head, Ordering::Release);
        let next = self.generation.load(Ordering::Relaxed).wrapping_add(1);
        self.generation.store(next, Ordering::Release);
        next
    }

    pub fn push(&self, samples: &[f32]) -> usize {
        let head = self.head.load(Ordering::Relaxed);
        let tail = self.tail.load(Ordering::Acquire);
        let used = head.wrapping_sub(tail).min(AUDIO_SAMPLE_CAPACITY as u64);
        let room = (AUDIO_SAMPLE_CAPACITY as u64).saturating_sub(used) as usize;
        let take = room.min(samples.len());
        let mut pushed = 0usize;
        for (offset, value) in samples.iter().take(take).enumerate() {
            let index = head.wrapping_add(offset as u64);
            if let Some(cell) = self.samples.get(ring_slot(index, AUDIO_SAMPLE_CAPACITY)) {
                cell.store(value.to_bits(), Ordering::Relaxed);
                pushed = pushed.wrapping_add(1);
            }
        }
        self.head
            .store(head.wrapping_add(pushed as u64), Ordering::Release);
        pushed
    }

    pub fn write_position(&self) -> u64 {
        self.head.load(Ordering::Relaxed)
    }

    pub fn push_marker(&self, marker: AudioMarker) -> bool {
        let head = self.marker_head.load(Ordering::Relaxed);
        let tail = self.marker_tail.load(Ordering::Acquire);
        if head.wrapping_sub(tail) >= AUDIO_MARKER_CAPACITY as u64 {
            return false;
        }
        let Some(slot) = self.markers.get(ring_slot(head, AUDIO_MARKER_CAPACITY)) else {
            return false;
        };
        slot.position.store(marker.position, Ordering::Relaxed);
        slot.pts.store(marker.pts.to_bits(), Ordering::Relaxed);
        self.marker_head
            .store(head.wrapping_add(1), Ordering::Release);
        true
    }

    pub fn occupied(&self) -> Result<usize, Fault> {
        let tail = self.tail.load(Ordering::Relaxed);
        let head = self.head.load(Ordering::Acquire);
        Ok(filled("audio", head, tail, AUDIO_SAMPLE_CAPACITY)? as usize)
    }

    pub fn pop_into(&self, out: &mut [f32]) -> Result<usize, Fault> {
        let tail = self.tail.load(Ordering::Relaxed);
        let head = self.head.load(Ordering::Acquire);
        let available = filled("audio", head, tail, AUDIO_SAMPLE_CAPACITY)? as usize;
        let take = available.min(out.len());
        let mut copied = 0usize;
        for (offset, destination) in out.iter_mut().take(take).enumerate() {
            let index = tail.wrapping_add(offset as u64);
            if let Some(cell) = self.samples.get(ring_slot(index, AUDIO_SAMPLE_CAPACITY)) {
                *destination = f32::from_bits(cell.load(Ordering::Relaxed));
                copied = copied.wrapping_add(1);
            }
        }
        self.tail
            .store(tail.wrapping_add(copied as u64), Ordering::Release);
        Ok(copied)
    }

    pub fn skip(&self, count: usize) -> Result<usize, Fault> {
        let tail = self.tail.load(Ordering::Relaxed);
        let head = self.head.load(Ordering::Acquire);
        let available = filled("audio", head, tail, AUDIO_SAMPLE_CAPACITY)? as usize;
        let dropped = available.min(count);
        self.tail
            .store(tail.wrapping_add(dropped as u64), Ordering::Release);
        Ok(dropped)
    }

    pub fn read_position(&self) -> u64 {
        self.tail.load(Ordering::Relaxed)
    }

    pub fn take_marker(&self) -> Result<Option<AudioMarker>, Fault> {
        let tail = self.marker_tail.load(Ordering::Relaxed);
        let head = self.marker_head.load(Ordering::Acquire);
        if filled("audio-marker", head, tail, AUDIO_MARKER_CAPACITY)? == 0 {
            return Ok(None);
        }
        let Some(slot) = self.markers.get(ring_slot(tail, AUDIO_MARKER_CAPACITY)) else {
            return Err(Fault::Ring {
                region: "audio-marker",
                head,
                tail,
                capacity: AUDIO_MARKER_CAPACITY as u64,
            });
        };
        let position = slot.position.load(Ordering::Relaxed);
        let pts = f64::from_bits(slot.pts.load(Ordering::Relaxed));
        self.marker_tail
            .store(tail.wrapping_add(1), Ordering::Release);
        if !pts.is_finite() {
            return Ok(None);
        }
        Ok(Some(AudioMarker { position, pts }))
    }
}

#[repr(u32)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum LogLevel {
    Error = 0,
    Warning = 1,
    Info = 2,
}

impl LogLevel {
    const fn from_wire(value: u32) -> Self {
        match value {
            0 => Self::Error,
            1 => Self::Warning,
            _ => Self::Info,
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct LogEntry {
    pub level: LogLevel,
    pub text: String,
}

#[repr(C)]
pub struct LogSlot {
    level: AtomicU32,
    len: AtomicU32,
    text: [AtomicU8; LOG_TEXT_BYTES],
}

const _: () = assert!(size_of::<LogSlot>() == 256);

#[repr(C)]
pub struct LogRing {
    head: Cursor,
    tail: Cursor,
    slots: [LogSlot; LOG_RING_CAPACITY],
}

impl LogRing {
    pub fn emit(&self, level: LogLevel, text: &str) -> bool {
        let head = self.head.load(Ordering::Relaxed);
        let tail = self.tail.load(Ordering::Acquire);
        if head.wrapping_sub(tail) >= LOG_RING_CAPACITY as u64 {
            return false;
        }
        let Some(slot) = self.slots.get(ring_slot(head, LOG_RING_CAPACITY)) else {
            return false;
        };
        let bytes = text.as_bytes();
        let len = bytes.len().min(LOG_TEXT_BYTES);
        for (cell, byte) in slot.text.iter().zip(bytes.iter().take(len)) {
            cell.store(*byte, Ordering::Relaxed);
        }
        slot.level.store(level as u32, Ordering::Relaxed);
        slot.len.store(len as u32, Ordering::Relaxed);
        self.head.store(head.wrapping_add(1), Ordering::Release);
        true
    }

    pub fn take(&self) -> Result<Option<LogEntry>, Fault> {
        let tail = self.tail.load(Ordering::Relaxed);
        let head = self.head.load(Ordering::Acquire);
        if filled("log", head, tail, LOG_RING_CAPACITY)? == 0 {
            return Ok(None);
        }
        let Some(slot) = self.slots.get(ring_slot(tail, LOG_RING_CAPACITY)) else {
            return Err(Fault::Ring {
                region: "log",
                head,
                tail,
                capacity: LOG_RING_CAPACITY as u64,
            });
        };
        let level = LogLevel::from_wire(slot.level.load(Ordering::Relaxed));
        let len = (slot.len.load(Ordering::Relaxed) as usize).min(LOG_TEXT_BYTES);
        let mut bytes = Vec::with_capacity(len);
        for cell in slot.text.iter().take(len) {
            bytes.push(cell.load(Ordering::Relaxed));
        }
        self.tail.store(tail.wrapping_add(1), Ordering::Release);
        Ok(Some(LogEntry {
            level,
            text: sanitise_text(&bytes),
        }))
    }
}

fn sanitise_text(bytes: &[u8]) -> String {
    String::from_utf8_lossy(bytes)
        .chars()
        .map(|c| if c.is_control() { '.' } else { c })
        .collect()
}

#[repr(C, align(128))]
pub struct SegmentHeader {
    magic: AtomicU64,
    version: AtomicU32,
    reserved: AtomicU32,
    bytes: AtomicU64,
    cookie: AtomicU64,
    host_pid: AtomicU32,
    helper_pid: AtomicU32,
}

#[repr(C, align(128))]
pub struct SharedSegment {
    pub header: SegmentHeader,
    pub transport: Transport,
    pub media: MediaFacts,
    pub cancel: CancelFlag,
    pub seek: SeekSlot,
    pub open: OpenSlot,
    pub audio_options: AudioOptionsCell,
    pub video: VideoRing,
    pub release: ReleaseRing,
    pub verify: VerifyRing,
    pub log: LogRing,
    pub audio: AudioRing,
    pub audio_format: AudioFormatCell,
    pub controls: ControlsCell,
    pub controls_echo: ControlsEcho,
    pub protocol_whitelist: ProtocolWhitelistCell,
    pub adapter: AdapterCell,
    pub fetch_request: FetchRequestCell,
    pub fetch_response: FetchResponseCell,
    pub fetch_bulk: FetchBulk,
}

pub const SEGMENT_BYTES: usize = size_of::<SharedSegment>();

impl SharedSegment {
    pub fn initialise(&self, host_pid: u32, cookie: u64) {
        self.header.version.store(PROTOCOL_VERSION, Ordering::Relaxed);
        self.header.bytes.store(SEGMENT_BYTES as u64, Ordering::Relaxed);
        self.header.cookie.store(cookie, Ordering::Relaxed);
        self.header.host_pid.store(host_pid, Ordering::Relaxed);
        self.header.magic.store(PROTOCOL_MAGIC, Ordering::Release);
    }

    pub fn attach(&self, helper_pid: u32, expected_cookie: u64) -> Result<(), Fault> {
        if self.header.magic.load(Ordering::Acquire) != PROTOCOL_MAGIC {
            return Err(Fault::Attach { what: "bad magic" });
        }
        if self.header.version.load(Ordering::Relaxed) != PROTOCOL_VERSION {
            return Err(Fault::Attach {
                what: "version mismatch",
            });
        }
        if self.header.bytes.load(Ordering::Relaxed) != SEGMENT_BYTES as u64 {
            return Err(Fault::Attach {
                what: "size mismatch",
            });
        }
        if self.header.cookie.load(Ordering::Relaxed) != expected_cookie {
            return Err(Fault::Attach {
                what: "cookie mismatch",
            });
        }
        self.header.helper_pid.store(helper_pid, Ordering::Release);
        Ok(())
    }

    pub fn helper_pid(&self) -> u32 {
        self.header.helper_pid.load(Ordering::Acquire)
    }

    pub fn boxed_zeroed() -> Box<Self> {
        unsafe { Box::<Self>::new_zeroed().assume_init() }
    }

    pub unsafe fn from_mapping<'a>(pointer: *mut u8, bytes: usize) -> Result<&'a Self, Fault> {
        if bytes < SEGMENT_BYTES {
            return Err(Fault::Attach {
                what: "mapping smaller than the segment",
            });
        }
        if !pointer.cast::<Self>().is_aligned() {
            return Err(Fault::Attach {
                what: "mapping is not suitably aligned",
            });
        }
        Ok(unsafe { &*pointer.cast::<Self>() })
    }
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    clippy::float_cmp,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              exists to keep them out of the shipped code paths"
)]
mod tests {
    use super::*;

    fn geometry() -> SurfaceGeometry {
        SurfaceGeometry {
            plane_width: [1920, 960],
            plane_height: [1080, 540],
            plane_count: 2,
        }
    }

    fn record(sequence: u64, slot: u32) -> FrameRecord {
        FrameRecord {
            info: FrameInfoWire {
                yuv_to_rgb: [1.0; 12],
                uv_transform: [1.0, 0.0, 0.0, 0.0, -1.0, 1.0],
                visible_width: 1920,
                visible_height: 1080,
                plane_width: [1920, 960],
                plane_height: [1080, 540],
                colorspace: 1,
                color_range: 1,
                color_primaries: 1,
                rotation: 0,
                bit_depth: 8,
            },
            flags: FRAME_FLAG_HAS_PTS,
            pts: 0.25,
            sequence,
            slot,
            reserved: 0,
        }
    }

    #[test]
    fn frame_info_is_the_wire_prefix_plus_three_host_fields() {
        let wire = record(7, 3).info;
        let frame = ValidFrame {
            sequence: 7,
            slot: 3,
            pts: Some(0.25),
            info: wire,
        };
        let info = assemble(&frame, 11, 2, [0xaaaa, 0xbbbb]);

        assert_eq!(size_of::<uuav_abi::FrameInfo>(), FRAME_INFO_BYTES);
        assert_eq!(info.visible_width, wire.visible_width);
        assert_eq!(info.bit_depth, wire.bit_depth);
        assert_eq!(info.frame_index, 11);
        assert_eq!(info.surface_generation, 2);
        assert_eq!(info.planes, [0xaaaa, 0xbbbb]);

        let bytes: [u8; 152] = unsafe { std::mem::transmute(info) };
        let prefix: [u8; 116] = unsafe { std::mem::transmute(wire) };
        assert_eq!(&bytes[..116], &prefix[..]);
    }

    #[test]
    fn layout_report() {
        println!("FrameInfoWire      {:>9} B", size_of::<FrameInfoWire>());
        println!("FrameRecord        {:>9} B", size_of::<FrameRecord>());
        println!("FrameSlot          {:>9} B", size_of::<FrameSlot>());
        println!("LogSlot            {:>9} B", size_of::<LogSlot>());
        println!("Transport          {:>9} B", size_of::<Transport>());
        println!("VideoRing          {:>9} B", size_of::<VideoRing>());
        println!("ReleaseRing        {:>9} B", size_of::<ReleaseRing>());
        println!("VerifyRing         {:>9} B", size_of::<VerifyRing>());
        println!("LogRing            {:>9} B", size_of::<LogRing>());
        println!("AudioRing          {:>9} B", size_of::<AudioRing>());
        println!("SharedSegment      {:>9} B", SEGMENT_BYTES);
    }

    #[test]
    fn frame_record_survives_the_atomic_word_image() {
        let original = record(7, 3);
        assert_eq!(FrameRecord::from_words(original.to_words()), original);
    }

    #[test]
    fn segment_is_a_sane_size() {
        const { assert!(SEGMENT_BYTES > 2 * 1024 * 1024) };
        const { assert!(SEGMENT_BYTES < 4 * 1024 * 1024) };
    }

    #[test]
    fn video_ring_round_trips_and_backpressures() {
        let segment = SharedSegment::boxed_zeroed();
        for sequence in 1..=VIDEO_RING_CAPACITY as u64 {
            assert!(segment.video.publish(&record(sequence, 0)));
        }
        assert!(
            !segment.video.publish(&record(99, 0)),
            "a full ring must refuse, not overwrite"
        );
        assert_eq!(segment.video.depth().unwrap(), VIDEO_RING_CAPACITY as u64);

        for sequence in 1..=VIDEO_RING_CAPACITY as u64 {
            let peeked = segment.video.peek().unwrap().unwrap();
            assert_eq!(peeked.sequence, sequence);
            assert_eq!(segment.video.peek().unwrap().unwrap().sequence, sequence);
            segment.video.commit();
        }
        assert!(segment.video.peek().unwrap().is_none());
    }

    #[test]
    fn a_head_beyond_the_capacity_is_corruption_not_arithmetic() {
        let segment = SharedSegment::boxed_zeroed();
        segment.video.head.store(9_999, Ordering::Release);
        let fault = segment.video.peek().expect_err("must be rejected");
        assert!(matches!(fault, Fault::Ring { region: "video", .. }), "{fault}");

        segment.audio.head.store(u64::MAX, Ordering::Release);
        assert!(segment.audio.occupied().is_err());
        let mut out = [0.0f32; 8];
        assert!(segment.audio.pop_into(&mut out).is_err());
    }

    #[test]
    fn a_wrapped_tail_is_not_corruption() {
        let segment = SharedSegment::boxed_zeroed();
        segment.video.head.store(2, Ordering::Release);
        segment.video.tail.store(u64::MAX, Ordering::Release);
        assert_eq!(segment.video.depth().unwrap(), 3);
    }

    #[test]
    fn validation_rejects_a_plane_larger_than_the_surface() {
        let mut hostile = record(1, 0);
        hostile.info.plane_width = [4096, 960];
        let fault = hostile
            .validate(0, &geometry())
            .expect_err("must be rejected");
        assert_eq!(
            fault,
            FrameFault::PlaneExceedsSurface {
                plane: 0,
                claimed: 4096,
                actual: 1920,
            }
        );
    }

    #[test]
    fn validation_rejects_the_obvious_lies() {
        let cases: [(FrameRecord, FrameFault); 5] = [
            (
                {
                    let mut r = record(1, SURFACE_SLOT_COUNT as u32);
                    r.slot = SURFACE_SLOT_COUNT as u32;
                    r
                },
                FrameFault::SlotOutOfRange {
                    slot: SURFACE_SLOT_COUNT as u32,
                },
            ),
            (
                record(5, 0),
                FrameFault::SequenceNotAdvancing { got: 5, last: 5 },
            ),
            (
                {
                    let mut r = record(1, 0);
                    r.info.bit_depth = 12;
                    r
                },
                FrameFault::BadBitDepth { bit_depth: 12 },
            ),
            (
                {
                    let mut r = record(1, 0);
                    r.info.rotation = 45;
                    r
                },
                FrameFault::BadRotation { rotation: 45 },
            ),
            (
                {
                    let mut r = record(1, 0);
                    r.pts = f64::NAN;
                    r
                },
                FrameFault::NonFinitePts,
            ),
        ];
        for (index, (hostile, expected)) in cases.into_iter().enumerate() {
            let last = if index == 1 { 5 } else { 0 };
            assert_eq!(
                hostile.validate(last, &geometry()).expect_err("rejected"),
                expected
            );
        }
    }

    #[test]
    fn validation_sanitises_shader_constants() {
        let mut hostile = record(1, 0);
        hostile.info.yuv_to_rgb[0] = f32::NAN;
        hostile.info.yuv_to_rgb[1] = 1.0e30;
        hostile.info.uv_transform[0] = f32::NEG_INFINITY;
        hostile.info.colorspace = 9_999;
        let valid = hostile.validate(0, &geometry()).unwrap();
        assert_eq!(valid.info.yuv_to_rgb[0], 0.0);
        assert_eq!(valid.info.yuv_to_rgb[1], MAX_SHADER_CONSTANT);
        assert_eq!(valid.info.uv_transform[0], 0.0);
        assert_eq!(valid.info.colorspace, AVCOL_SPC_UNSPECIFIED);
        assert_eq!(valid.slot, 0);
        assert_eq!(valid.pts, Some(0.25));
    }

    #[test]
    fn transport_round_trips_and_rejects_a_bogus_state() {
        let segment = SharedSegment::boxed_zeroed();
        let anchor = uptime_nanos();
        let snapshot = TransportSnapshot {
            state: PlaybackState::Playing,
            clock: ClockWire {
                base: 2.0,
                anchor_nanos: anchor,
                rate: 1.0,
            },
        };
        segment.transport.publish(snapshot);
        assert_eq!(segment.transport.read(), TransportRead::Fresh(snapshot));

        segment.transport.0.write(&[42, 0, 0, 1.0f64.to_bits()]);
        assert_eq!(
            segment.transport.read(),
            TransportRead::Corrupt(Fault::Transport { state: 42 })
        );
    }

    #[test]
    fn a_seqlock_left_mid_write_does_not_spin_the_reader() {
        let segment = SharedSegment::boxed_zeroed();
        segment.transport.publish(TransportSnapshot {
            state: PlaybackState::Ready,
            clock: ClockWire::HELD_AT_ZERO,
        });
        segment.transport.0.sequence.store(3, Ordering::Release);
        assert_eq!(segment.transport.read(), TransportRead::Contended);
    }

    #[test]
    fn a_hostile_clock_cannot_run_backwards_or_produce_nan() {
        let now = uptime_nanos();
        let future = ClockWire {
            base: 1.0,
            anchor_nanos: now.saturating_add(60_000_000_000),
            rate: 1.0,
        };
        assert_eq!(future.now(now), 1.0);

        let poisoned = ClockWire {
            base: f64::NAN,
            anchor_nanos: now,
            rate: f64::INFINITY,
        }
        .sanitised();
        assert_eq!(poisoned.base, 0.0);
        assert_eq!(poisoned.rate, 1.0);
        assert!(poisoned.now(now.saturating_add(1_000_000)).is_finite());
    }

    #[test]
    fn audio_ring_wraps_and_reports_what_it_copied() {
        let segment = SharedSegment::boxed_zeroed();
        let near_wrap = (AUDIO_SAMPLE_CAPACITY as u64).wrapping_sub(3);
        segment.audio.head.store(near_wrap, Ordering::Release);
        segment.audio.tail.store(near_wrap, Ordering::Release);

        let input: Vec<f32> = (0..8).map(|i| i as f32).collect();
        assert_eq!(segment.audio.push(&input), 8);
        let mut out = [-1.0f32; 8];
        assert_eq!(segment.audio.pop_into(&mut out).unwrap(), 8);
        assert_eq!(out.to_vec(), input);
        assert_eq!(segment.audio.occupied().unwrap(), 0);

        let mut empty = [-1.0f32; 4];
        assert_eq!(segment.audio.pop_into(&mut empty).unwrap(), 0);
        assert_eq!(empty, [-1.0f32; 4]);
    }

    #[test]
    fn log_text_from_a_hostile_helper_is_safe_to_hand_to_mono() {
        let segment = SharedSegment::boxed_zeroed();
        assert!(segment.log.emit(LogLevel::Error, "a\0b\nc\u{1b}[2Jd"));
        let entry = segment.log.take().unwrap().unwrap();
        assert_eq!(entry.level, LogLevel::Error);
        assert_eq!(entry.text, "a.b.c.[2Jd");
        assert!(CString::new(entry.text).is_ok());

        segment.log.emit(LogLevel::Info, "short");
        let slot = &segment.log.slots[ring_slot(1, LOG_RING_CAPACITY)];
        slot.len.store(u32::MAX, Ordering::Relaxed);
        let entry = segment.log.take().unwrap().unwrap();
        assert_eq!(entry.text.len(), LOG_TEXT_BYTES);
    }

    #[test]
    fn open_slot_refuses_an_over_long_url_and_survives_a_generation_race() {
        let segment = SharedSegment::boxed_zeroed();
        assert!(segment.open.publish(&"x".repeat(URL_MAX_BYTES + 1)).is_none());

        let generation = segment.open.publish("file:///tmp/a.mp4").unwrap();
        assert_eq!(
            segment.open.take(generation).as_deref(),
            Some("file:///tmp/a.mp4")
        );
        let newer = segment.open.publish("file:///tmp/b.mp4").unwrap();
        assert!(segment.open.take(generation).is_none(), "latest wins");
        assert_eq!(
            segment.open.take(newer).as_deref(),
            Some("file:///tmp/b.mp4")
        );
    }

    #[test]
    fn release_ring_holds_everything_a_correct_host_can_owe() {
        let segment = SharedSegment::boxed_zeroed();
        for sequence in 0..(VIDEO_RING_CAPACITY + RETAINED_FRAMES) as u64 {
            assert!(segment.release.release(sequence));
        }
        for sequence in 0..(VIDEO_RING_CAPACITY + RETAINED_FRAMES) as u64 {
            assert_eq!(segment.release.take().unwrap(), Some(sequence));
        }
        assert_eq!(segment.release.take().unwrap(), None);
    }

    #[test]
    fn seek_is_coalescing_and_cancel_is_visible_both_ways() {
        let segment = SharedSegment::boxed_zeroed();
        assert!(!segment.seek.is_pending());
        segment.seek.request(1.0);
        segment.seek.request(2.0);
        assert!(segment.seek.is_pending());
        assert_eq!(segment.seek.take(), Some(2.0));
        assert!(!segment.seek.is_pending());
        assert_eq!(segment.seek.take(), None);
        segment.seek.request(f64::NAN);
        assert_eq!(segment.seek.take(), Some(0.0));

        assert!(!segment.cancel.is_set());
        segment.cancel.set();
        assert!(segment.cancel.is_set());
    }

    #[test]
    fn verify_entries_answer_only_for_the_sequence_they_hold() {
        let segment = SharedSegment::boxed_zeroed();
        segment.verify.publish(VerifyEntry {
            sequence: 5,
            checksum: 0xdead_beef,
            byte_count: 1024,
            decode_ready_nanos: 77,
            published_nanos: 78,
        });
        assert_eq!(
            segment.verify.lookup(5),
            Some(VerifyEntry {
                sequence: 5,
                checksum: 0xdead_beef,
                byte_count: 1024,
                decode_ready_nanos: 77,
                published_nanos: 78
            })
        );
        assert_eq!(segment.verify.lookup(6), None);
        assert_eq!(
            segment.verify.lookup(5 + VIDEO_RING_CAPACITY as u64),
            None,
            "a video-ring-sized wrap must not reach the entry"
        );
        assert!(segment.verify.lookup(5).is_some());
        segment.verify.publish(VerifyEntry {
            sequence: 5 + VERIFY_RING_CAPACITY as u64,
            checksum: 1,
            byte_count: 1,
            decode_ready_nanos: 0,
            published_nanos: 0,
        });
        assert_eq!(segment.verify.lookup(5), None);
    }

    #[test]
    fn media_facts_reject_impossible_geometry() {
        let segment = SharedSegment::boxed_zeroed();
        assert_eq!(segment.media.read().unwrap(), None);
        let facts = MediaFactsValue {
            open_generation: 1,
            duration: 12.5,
            visible_width: 1920,
            visible_height: 1080,
            has_video: true,
            has_audio: false,
            sample_rate: 0,
            channels: 0,
        };
        segment.media.publish(facts);
        assert_eq!(segment.media.read().unwrap(), Some(facts));

        segment.media.publish(MediaFactsValue {
            visible_width: 100_000,
            ..facts
        });
        assert!(segment.media.read().is_err());
    }

    #[test]
    fn message_kinds_are_directional_and_rights_are_gated() {
        assert!(check_incoming(kind::OPENED, false).is_ok());
        assert!(check_incoming(kind::SURFACE, true).is_ok());
        assert!(check_incoming(kind::FETCH, false).is_ok());
        assert!(check_incoming(kind::FETCH, true).is_err());
        assert!(check_incoming(kind::PLAY, false).is_err());
        assert!(check_incoming(kind::OPENED, true).is_err());
        assert!(check_incoming(kind::SURFACE, false).is_err());
        assert!(check_incoming(0xffff_ffff, false).is_err());
        assert!(is_host_to_helper(kind::SHUTDOWN));
        assert!(!is_host_to_helper(kind::HELLO));
    }

    #[test]
    fn every_admitted_helper_kind_classifies_after_the_handshake() {
        for value in 0u32..=0x1ff {
            let admitted = check_incoming(value, carries_right(value)).is_ok();
            let classified = classify_helper_message(value).is_ok();
            if value == kind::HELLO {
                assert!(admitted, "HELLO is a legal helper kind");
                assert!(!classified, "a second HELLO must fault");
            } else {
                assert_eq!(
                    admitted, classified,
                    "kind {value:#04x}: check_incoming and the dispatchers disagree"
                );
            }
        }
        assert_eq!(
            classify_helper_message(kind::FETCH),
            Ok(HelperEvent::FetchDoorbell),
            "the fetch doorbell must be acknowledged, never faulted"
        );
    }

    #[test]
    fn names_fit_the_platform_limits() {
        let segment = segment_name(99_999, 0x0123_4567_89ab_cdef).unwrap();
        assert!(segment.as_bytes().len() <= SHM_NAME_MAX, "{segment:?}");
        assert!(service_name(99_999, 1).is_ok());
        assert_ne!(nonce(), nonce());
    }

    #[test]
    fn the_checksum_covers_visible_pixels_only() {
        let plane = vec![0xaa_u8; 6 * 2];
        let (with_padding, covered_padding) = checksum_luma(&plane, 6, 6, 2);
        let (visible_only, covered_visible) = checksum_luma(&plane, 6, 4, 2);
        assert_ne!(with_padding, visible_only);
        assert_eq!(covered_padding, 12);
        assert_eq!(covered_visible, 8);
        let (_, covered_short) = checksum_luma(&plane, 6, 4, 99);
        assert_eq!(covered_short, 8);
        assert!(u32::try_from(with_padding).is_ok());
    }

    #[test]
    fn the_checksum_reacts_to_one_flipped_bit_anywhere() {
        let base = vec![0x40_u8; 64 * 8];
        let (reference, _) = checksum_luma(&base, 64, 64, 8);
        for position in [0usize, 63, 64 * 4 + 7, 64 * 8 - 1] {
            let mut mutated = base.clone();
            mutated[position] ^= 0x01;
            let (changed, _) = checksum_luma(&mutated, 64, 64, 8);
            assert_ne!(changed, reference, "byte {position} went unnoticed");
        }
        let mut swapped_in_row = base;
        swapped_in_row[0] = 0x41;
        swapped_in_row[1] = 0x3f;
        let (in_row, _) = checksum_luma(&swapped_in_row, 64, 64, 8);
        assert_ne!(in_row, reference);
    }

    #[test]
    fn degenerate_geometry_is_fatal_regardless_of_import_path() {
        let one_plane = SurfaceGeometry {
            plane_width: [64, 0],
            plane_height: [64, 0],
            plane_count: 1,
        };
        assert!(matches!(
            record(1, 0).validate(0, &one_plane),
            Err(FrameFault::TooFewPlanes { plane_count: 1 })
        ));

        let no_planes = SurfaceGeometry {
            plane_width: [0, 0],
            plane_height: [0, 0],
            plane_count: 0,
        };
        assert!(matches!(
            record(1, 0).validate(0, &no_planes),
            Err(FrameFault::TooFewPlanes { plane_count: 0 })
        ));

        let mut zero_luma = record(1, 0);
        zero_luma.info.plane_width = [0, 0];
        assert!(zero_luma.validate(0, &geometry()).is_err());
        let mut zero_chroma = record(1, 0);
        zero_chroma.info.plane_height = [1080, 0];
        assert_eq!(
            zero_chroma.validate(0, &geometry()).expect_err("rejected"),
            FrameFault::BadDimension { value: 0 }
        );
    }

    #[test]
    fn the_chroma_plane_bound_is_always_enforced() {
        let mut hostile = record(1, 0);
        hostile.info.plane_width[1] = 4096;
        assert_eq!(
            hostile.validate(0, &geometry()).expect_err("rejected"),
            FrameFault::PlaneExceedsSurface {
                plane: 1,
                claimed: 4096,
                actual: 960,
            }
        );
    }

    #[test]
    fn audio_format_cell_round_trips_and_is_unpublished_when_zeroed() {
        let segment = SharedSegment::boxed_zeroed();
        assert_eq!(segment.audio_format.read(), None);
        segment.audio_format.publish(48_000, 6, 3, 9);
        assert_eq!(segment.audio_format.read(), Some((48_000, 6, 3, 9)));
        segment.audio_format.publish(44_100, 2, 4, 10);
        assert_eq!(segment.audio_format.read(), Some((44_100, 2, 4, 10)));
    }

    #[test]
    fn controls_cell_looping_rate_and_master_track_generations() {
        let segment = SharedSegment::boxed_zeroed();
        assert!(!segment.controls.looping());
        assert_eq!(segment.controls.requested_rate(), (1.0, 0));
        assert_eq!(segment.controls.master_clock(), None);

        segment.controls.set_looping(true);
        assert!(segment.controls.looping());

        assert_eq!(segment.controls.request_rate(2.0), 1);
        assert_eq!(segment.controls.requested_rate(), (2.0, 1));
        assert_eq!(segment.controls.request_rate(f64::NAN), 2);
        assert_eq!(segment.controls.requested_rate(), (1.0, 2));
        assert_eq!(segment.controls.request_rate(100.0), 3);
        assert_eq!(segment.controls.requested_rate(), (1.0, 3));

        assert_eq!(segment.controls.request_master_clock(4.5), 1);
        assert_eq!(segment.controls.master_clock(), Some((4.5, 1)));
        assert_eq!(segment.controls.request_master_clock(f64::INFINITY), 2);
        assert_eq!(segment.controls.master_clock(), Some((0.0, 2)));
    }

    #[test]
    fn controls_echo_reports_applied_state() {
        let segment = SharedSegment::boxed_zeroed();
        assert_eq!(segment.controls_echo.read(), (false, 0));
        segment.controls_echo.publish(true, 7);
        assert_eq!(segment.controls_echo.read(), (true, 7));
    }

    #[test]
    fn protocol_whitelist_refuses_empty_over_long_and_non_token() {
        let segment = SharedSegment::boxed_zeroed();
        assert_eq!(segment.protocol_whitelist.read(), None);
        assert!(!segment.protocol_whitelist.publish(""));
        assert!(
            !segment
                .protocol_whitelist
                .publish(&"a".repeat(PROTOCOL_WHITELIST_MAX_BYTES + 1))
        );

        assert!(segment.protocol_whitelist.publish("https,tls,tcp,crypto,data"));
        assert_eq!(
            segment.protocol_whitelist.read().as_deref(),
            Some("https,tls,tcp,crypto,data")
        );

        for (cell, byte) in segment
            .protocol_whitelist
            .text
            .iter()
            .zip(b"file:///x".iter())
        {
            cell.store(*byte, Ordering::Relaxed);
        }
        segment
            .protocol_whitelist
            .len
            .store(b"file:///x".len() as u32, Ordering::Release);
        assert_eq!(segment.protocol_whitelist.read(), None);
    }

    #[test]
    fn the_added_cells_keep_the_segment_a_sane_size() {
        const { assert!(SEGMENT_BYTES > 2 * 1024 * 1024) };
        const { assert!(SEGMENT_BYTES < 4 * 1024 * 1024) };
        let segment = SharedSegment::boxed_zeroed();
        assert_eq!(segment.audio_format.read(), None);
        assert!(!segment.controls.looping());
        assert_eq!(segment.controls_echo.read(), (false, 0));
        assert_eq!(segment.protocol_whitelist.read(), None);
        assert_eq!(segment.adapter.read(), None);
    }

    #[test]
    fn the_adapter_cell_separates_unpublished_from_software() {
        let segment = SharedSegment::boxed_zeroed();

        assert_eq!(segment.adapter.read(), None);

        segment.adapter.publish(0);
        assert_eq!(segment.adapter.read(), None);

        segment.adapter.publish(0xFFFF_FFFF_0000_0001);
        assert_eq!(segment.adapter.read(), Some(0xFFFF_FFFF_0000_0001));
    }

    #[test]
    fn fetch_request_and_response_correlate_by_generation() {
        let segment = SharedSegment::boxed_zeroed();
        assert_eq!(segment.fetch_request.take(0), None);
        assert_eq!(segment.fetch_response.read(1), None);

        let g1 = segment
            .fetch_request
            .publish(fetch_op::OPEN, 0, 0, 0, 0, "https://cdn/movie.mp4")
            .unwrap();
        assert_eq!(g1, 1);
        let request = segment.fetch_request.take(0).unwrap();
        assert_eq!(request.op, fetch_op::OPEN);
        assert_eq!(request.url, "https://cdn/movie.mp4");
        assert_eq!(segment.fetch_request.take(g1), None);

        segment.fetch_response.publish(g1, fetch_status::OK, 0, 4096, 7);
        assert_eq!(
            segment.fetch_response.read(g1),
            Some(FetchResponse {
                status: fetch_status::OK,
                n: 0,
                size: 4096,
                out_handle: 7,
            })
        );

        let g2 = segment.fetch_request.publish(fetch_op::READ, 7, 100, 64, 0, "");
        let g2 = g2.unwrap();
        assert_eq!(g2, 2);
        let request = segment.fetch_request.take(g1).unwrap();
        assert_eq!(request.op, fetch_op::READ);
        assert_eq!((request.handle, request.offset, request.len), (7, 100, 64));

        let payload: Vec<u8> = (0..64u8).collect();
        let n = segment.fetch_bulk.stage(&payload);
        assert_eq!(n, 64);
        segment.fetch_response.publish(g2, fetch_status::OK, n, -1, 0);
        let response = segment.fetch_response.read(g2).unwrap();
        let mut out = vec![0u8; 200];
        let copied = segment.fetch_bulk.copy_out(response.n as usize, &mut out);
        assert_eq!(copied, 64);
        assert_eq!(&out[..64], &payload[..]);
    }

    #[test]
    fn fetch_request_bounds_the_child_chosen_lengths() {
        let segment = SharedSegment::boxed_zeroed();
        assert_eq!(
            segment
                .fetch_request
                .publish(fetch_op::OPEN, 0, 0, 0, 0, &"a".repeat(URL_MAX_BYTES + 1)),
            None
        );
        segment
            .fetch_request
            .publish(fetch_op::READ, 1, 0, FETCH_SLOT_BYTES as u32 + 4096, 0, "")
            .unwrap();
        assert_eq!(segment.fetch_request.take(0).unwrap().len, FETCH_SLOT_BYTES as u32);
    }

    #[test]
    fn an_abandoned_request_overwrite_never_yields_a_torn_request() {
        const ROUNDS: u64 = 150_000;
        const URL_LEN: usize = 512;

        fn url_for(generation: u64) -> String {
            let mut url = String::with_capacity(URL_LEN);
            for i in 0..URL_LEN {
                url.push(char::from(b'a' + ((generation as usize + i) % 26) as u8));
            }
            url
        }

        let segment = std::sync::Arc::new(SharedSegment::boxed_zeroed());
        let writer_segment = std::sync::Arc::clone(&segment);
        let writer = std::thread::spawn(move || {
            for expected in 1..=ROUNDS {
                let generation = writer_segment
                    .fetch_request
                    .publish(
                        fetch_op::READ,
                        expected as u32,
                        expected.wrapping_mul(0x9e37_79b9),
                        expected as u32 ^ 0xffff,
                        expected as u32 >> 1,
                        &url_for(expected),
                    )
                    .unwrap();
                assert_eq!(generation, expected);
            }
        });

        let mut last_seen = 0u64;
        let mut taken = 0u64;
        while last_seen < ROUNDS {
            let Some(request) = segment.fetch_request.take(last_seen) else {
                if writer.is_finished() && segment.fetch_request.take(last_seen).is_none() {
                    break;
                }
                continue;
            };
            let g = request.generation;
            assert!(g > last_seen, "generations move forward");
            assert_eq!(request.op, fetch_op::READ, "torn request at generation {g}");
            assert_eq!(request.handle, g as u32, "torn handle at generation {g}");
            assert_eq!(
                request.offset,
                g.wrapping_mul(0x9e37_79b9),
                "torn offset at generation {g}"
            );
            assert_eq!(
                request.len,
                (g as u32 ^ 0xffff).min(FETCH_SLOT_BYTES as u32),
                "torn len at generation {g}"
            );
            assert_eq!(request.flags, g as u32 >> 1, "torn flags at generation {g}");
            assert_eq!(request.url, url_for(g), "torn url at generation {g}");
            last_seen = g;
            taken += 1;
        }
        writer.join().unwrap();
        assert!(taken > 0, "the reader must have observed at least one request");
    }

    #[test]
    fn ticks_convert_to_nanos_without_overflowing() {
        assert_eq!(ticks_to_nanos(12_345, 0), 0);

        assert_eq!(ticks_to_nanos(10_000_000, 10_000_000), 1_000_000_000);
        assert_eq!(ticks_to_nanos(1, 10_000_000), 100);
        assert_eq!(ticks_to_nanos(15_000_000, 10_000_000), 1_500_000_000);

        for ticks in [0u64, 1, 7, 999_999, 3_000_000_000] {
            let hz = 10_000_000u64;
            assert_eq!(ticks_to_nanos(ticks, hz), ticks * 1_000_000_000 / hz);
        }

        let months = 10_000_000u64 * 60 * 60 * 24 * 90;
        assert!(months.checked_mul(1_000_000_000).is_none());
        assert_eq!(ticks_to_nanos(months, 10_000_000), 7_776_000_000_000_000);
    }
}
