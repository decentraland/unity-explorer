#![warn(clippy::all, clippy::pedantic, clippy::nursery)]
#![deny(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    clippy::todo,
    clippy::dbg_macro
)]
#![allow(
    clippy::uninlined_format_args,
    clippy::missing_errors_doc,
    clippy::option_if_let_else,
    clippy::single_match_else,
    clippy::must_use_candidate,
    clippy::future_not_send,
    clippy::enum_glob_use
)]
// FFI-heavy crate: raw-pointer borrows, numeric casts between C and media
// timestamp domains, and crate-private modules are pervasive by nature
// (doc_markdown: insists on backticks around the word "FFmpeg" in prose)
#![allow(
    clippy::borrow_as_ptr,
    clippy::redundant_pub_crate,
    clippy::cast_precision_loss,
    clippy::cast_sign_loss,
    clippy::cast_possible_truncation,
    clippy::cast_ptr_alignment,
    clippy::missing_safety_doc,
    clippy::doc_markdown
)]

mod audio_decoder;
mod ffutil;
mod playback;
mod player;

// Per-platform sibling modules: same names, same cross-platform-consumed
// surface, selected by target. Consumers stay platform-agnostic.
#[cfg(target_os = "windows")]
#[path = "hw_device_windows.rs"]
mod hw_device;
#[cfg(target_os = "macos")]
#[path = "hw_device_macos.rs"]
mod hw_device;

#[cfg(target_os = "windows")]
#[path = "video_decoder_windows.rs"]
mod video_decoder;
#[cfg(target_os = "macos")]
#[path = "video_decoder_macos.rs"]
mod video_decoder;

#[cfg(target_os = "windows")]
#[path = "video_output_windows.rs"]
mod video_output;
#[cfg(target_os = "macos")]
#[path = "video_output_macos.rs"]
mod video_output;

#[cfg(not(any(target_os = "windows", target_os = "macos")))]
compile_error!("uuav supports Windows (D3D11) and macOS (Metal) only");

use anyhow::{Context as _, ensure};
use arc_swap::{ArcSwap, ArcSwapOption};
use dashmap::DashMap;
use ffmpeg_sys_next as ff;
use ffutil::StreamingProtocol;
use hw_device::HwDevice;
use playback::DEFAULT_PLAYBACK_RATE;
use player::UUAVPlayer;
use std::{
    convert::AsRef,
    ffi::{CStr, CString},
    num::{NonZeroI32, NonZeroUsize},
    os::raw::{c_char, c_int, c_void},
    ptr,
    sync::{Arc, Once, Weak, atomic::AtomicU64},
};

const ERR_NO_RUNTIME: &str = "Runtime is not found";
const ERR_NO_PLAYER: &str = "player with specific id not found";

static INIT_STATE: ArcSwapOption<Runtime> = ArcSwapOption::const_empty();
static NEXT_STREAM_ID: AtomicU64 = AtomicU64::new(1);

// installs the FFmpeg log trampoline exactly once, process-wide
static LOG_INIT: Once = Once::new();

// FFmpeg `lavu_log_constants`; local copies avoid depending on binding names.
// Messages at or below these levels route to the matching Unity sink.
const AV_LOG_ERROR: c_int = 16;
const AV_LOG_WARNING: c_int = 24;

// Stack buffer for one formatted FFmpeg log line (prefix + message).
const LOG_LINE_CAP: c_int = 1024;

struct Runtime {
    device: HwDevice,
    error_callback: Arc<RawErrorCallback>,
    warning_callback: Arc<RawLogCallback>,
    log_callback: Arc<RawLogCallback>,
    audio_options: Arc<ArcSwap<AudioOptions>>,
    protocol_whitelist: Arc<StreamingProtocol>,
    registry: DashMap<PlayerId, UUAVPlayer>,
}

type RawErrorCallback = extern "C" fn(*const c_char);

// FFmpeg log sinks share the error-callback shape: a single NUL-terminated,
// already-formatted line. Level selects which sink native routes to.
type RawLogCallback = extern "C" fn(*const c_char);

// no-op when the raw callback is dropped on deinit
#[derive(Clone)]
struct ErrorCallback {
    callback: Weak<RawErrorCallback>,
}

impl ErrorCallback {
    pub fn from_raw(callback: Weak<RawErrorCallback>) -> Self {
        Self { callback }
    }

    pub fn report(&self, message: impl AsRef<str>) {
        if let Some(callback) = self.callback.upgrade() {
            let message: &str = message.as_ref();
            let c = CString::new(message).unwrap_or_default();
            callback(c.as_ptr());
        }
    }
}

pub type PlayerId = u64;

#[allow(non_camel_case_types)]
#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub enum UUAVState {
    UUAV_CLOSED = 0,
    UUAV_OPENING = 1,
    UUAV_READY = 2,
    UUAV_PLAYING = 3,
    UUAV_PAUSED = 4,
    UUAV_ENDED = 5,
    UUAV_ERROR = 6,
    UUAV_UNKNOWN = 7,
}

#[repr(C)]
#[derive(Default)]
pub struct Status {
    pub players_count: u64,
    pub initialized: bool,
    pub audio_options: AudioOptionsRaw,
    pub device_remove_reason: *const c_char,
}

/// Untrusted external input, must never be used for internals.
/// Always convert to AudioOptions to sanitize.
#[repr(C)]
#[derive(Default, Clone, Copy, PartialEq, Eq)]
pub struct AudioOptionsRaw {
    pub sample_rate: i32,
    pub channels: i32,
}

/// Sanitized input, guarantees to have values greater than zero
#[repr(C)]
#[derive(Clone, Copy, PartialEq, Eq)]
pub struct AudioOptions {
    pub sample_rate: NonZeroI32,
    pub channels: NonZeroI32,
}

impl AudioOptions {
    // AudioOptions is sanitized at the FFI boundary: always positive
    const fn channels_usize(self) -> NonZeroUsize {
        // SAFETY the channels value is checked at the construction
        unsafe { NonZeroUsize::new_unchecked(self.channels.get() as usize) }
    }

    fn sample_rate_f64(self) -> f64 {
        f64::from(self.sample_rate.get())
    }
}

impl TryFrom<AudioOptionsRaw> for AudioOptions {
    type Error = anyhow::Error;

    fn try_from(raw: AudioOptionsRaw) -> Result<Self, Self::Error> {
        ensure!(
            raw.sample_rate > 0,
            "sample_rate must be positive, got {}",
            raw.sample_rate
        );
        ensure!(
            raw.channels > 0,
            "channels must be positive, got {}",
            raw.channels
        );

        unsafe {
            let sample_rate = NonZeroI32::new_unchecked(raw.sample_rate);
            let channels = NonZeroI32::new_unchecked(raw.channels);
            Ok(Self {
                sample_rate,
                channels,
            })
        }
    }
}

impl From<AudioOptions> for AudioOptionsRaw {
    fn from(options: AudioOptions) -> Self {
        Self {
            sample_rate: options.sample_rate.get(),
            channels: options.channels.get(),
        }
    }
}

/// Read-only view of the engine's audio output configuration
/// For internal look-ups
#[derive(Clone)]
pub(crate) struct AudioOptionsView(Arc<ArcSwap<AudioOptions>>);

impl AudioOptionsView {
    pub(crate) fn current(&self) -> AudioOptions {
        **self.0.load()
    }
}

#[repr(C)]
#[derive(Clone)]
pub struct VideoSize {
    pub width: u32,
    pub height: u32,
}

/// Snapshot of the open media's source stream parameters.
///
/// Name fields are NUL-terminated UTF-8; =
/// unknown values are 0 / empty, `duration` is -1.0 for realtime streams.
/// Video fields are zero when `has_video` is 0,
/// audio fields when `has_audio` is 0.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct MediaInfo {
    pub duration: f64,
    pub framerate: f64,
    pub video_bitrate: i64,
    pub audio_bitrate: i64,
    pub width: u32,
    pub height: u32,
    pub sample_rate: i32,
    pub channels: i32,
    pub video_codec: [c_char; MEDIA_INFO_NAME_LEN],
    pub pixel_format: [c_char; MEDIA_INFO_NAME_LEN],
    pub audio_codec: [c_char; MEDIA_INFO_NAME_LEN],
    pub sample_format: [c_char; MEDIA_INFO_NAME_LEN],
    pub has_video: u8,
    pub has_audio: u8,
}

pub const MEDIA_INFO_NAME_LEN: usize = 32;

impl MediaInfo {
    pub(crate) const fn empty() -> Self {
        Self {
            duration: -1.0,
            framerate: 0.0,
            video_bitrate: 0,
            audio_bitrate: 0,
            width: 0,
            height: 0,
            sample_rate: 0,
            channels: 0,
            video_codec: [0; MEDIA_INFO_NAME_LEN],
            pixel_format: [0; MEDIA_INFO_NAME_LEN],
            audio_codec: [0; MEDIA_INFO_NAME_LEN],
            sample_format: [0; MEDIA_INFO_NAME_LEN],
            has_video: 0,
            has_audio: 0,
        }
    }
}

/// Snapshot of the player's cumulative audio pipeline counters.
///
/// Counters accumulate across url switches and only reset with the
/// player; `ring_fill_samples` is a gauge (occupancy after the last
/// read). `ring_stalls` grows in normal steady state — the decoded ring
/// runs full by design — and signals starvation only together with a
/// low `ring_fill_samples`.
#[repr(C)]
#[derive(Default, Clone, Copy)]
pub struct AudioPipelineStats {
    /// Interleaved samples deleted by drift correction (audio ran late
    /// against the master clock).
    pub drift_dropped_samples: u64,
    /// Reads answered with silence because audio ran ahead of the clock.
    pub silence_pulls: u64,
    /// Decoded frames that waited for ring space at least once.
    pub ring_stalls: u64,
    /// Decoded-ring occupancy after the last read, interleaved samples.
    pub ring_fill_samples: u64,
}

/// Snapshot of the user-facing control values: the latest pushed
/// intents, which the playback thread applies asynchronously.
///
/// A `*_pending` flag of 1 means that control's latest push has not
/// been consumed by the playback thread yet. Each value/pending pair
/// is consistent; the controls are sampled independently of each other.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct ControlsState {
    pub rate: f64,
    pub play: u8,
    pub play_pending: u8,
    pub looping: u8,
    pub looping_pending: u8,
    pub rate_pending: u8,
}

#[repr(C)]
pub struct NewPlayerResult {
    pub player_id: PlayerId,
    pub error_message: *const c_char,
}

impl NewPlayerResult {
    const fn ok(player_id: PlayerId) -> Self {
        Self {
            player_id,
            error_message: ptr::null(),
        }
    }

    fn error(message: impl AsRef<str>) -> Self {
        Self {
            player_id: 0,
            error_message: string_to_c_bytes(message),
        }
    }
}

#[repr(C)]
pub struct ResultFFI {
    pub error_message: *const c_char,
}

impl ResultFFI {
    const fn ok() -> Self {
        Self {
            error_message: ptr::null(),
        }
    }

    fn error(message: &str) -> Self {
        Self {
            error_message: string_to_c_bytes(message),
        }
    }
}

impl<T> From<anyhow::Result<T>> for ResultFFI {
    fn from(value: anyhow::Result<T>) -> Self {
        match value {
            Ok(_) => Self::ok(),
            Err(e) => Self::error(e.to_string().as_str()),
        }
    }
}

impl From<anyhow::Error> for ResultFFI {
    fn from(err: anyhow::Error) -> Self {
        Self::error(err.to_string().as_str())
    }
}

fn string_to_c_bytes(s: impl AsRef<str>) -> *const c_char {
    CString::new(s.as_ref()).unwrap_or_default().into_raw()
}

impl Runtime {
    /// Shared-access guard over a registered player; the player stays
    /// solely owned by the registry, so the guard cannot outlive it.
    fn player_by_id(
        &self,
        player_id: PlayerId,
    ) -> anyhow::Result<dashmap::mapref::one::Ref<'_, PlayerId, UUAVPlayer>> {
        self.registry.get(&player_id).context(ERR_NO_PLAYER)
    }

    /// Exclusive-access counterpart of [`Self::player_by_id`].
    fn player_by_id_mut(
        &self,
        player_id: PlayerId,
    ) -> anyhow::Result<dashmap::mapref::one::RefMut<'_, PlayerId, UUAVPlayer>> {
        self.registry.get_mut(&player_id).context(ERR_NO_PLAYER)
    }
}

/// releases an error message
/// must be called exactly once per non-null message
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_string_free(string: *mut c_char) {
    if string.is_null() {
        return;
    }

    drop(unsafe { CString::from_raw(string) });
}

#[unsafe(no_mangle)]
pub const extern "C" fn uuav_abi_version() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), '\0').as_ptr().cast()
}

/// This target's `va_list` as it appears in the bindgen signatures of
/// `av_log_set_callback`/`av_log_format_line2`: a plain `char*` on
/// `x86_64-pc-windows-gnu` and `aarch64-apple-darwin`, a pointer to the
/// SysV `__va_list_tag` on `x86_64-apple-darwin`.
#[cfg(all(target_arch = "x86_64", target_os = "macos"))]
type FfmpegVaList = *mut ff::__va_list_tag;
#[cfg(not(all(target_arch = "x86_64", target_os = "macos")))]
type FfmpegVaList = *mut c_char;

/// Process-global FFmpeg log callback installed via `av_log_set_callback`.
///
/// Fires from arbitrary (possibly concurrent) FFmpeg threads. It no-ops once
/// the runtime is torn down, applies the configured verbosity threshold itself
/// (FFmpeg leaves that to the callback), formats the line — including the
/// `AVClass`-derived `[component @ 0x..]` prefix — via `av_log_format_line2`,
/// then routes to the Unity sink matching the severity.
unsafe extern "C" fn uuav_ffmpeg_log(
    avcl: *mut c_void,
    level: c_int,
    fmt: *const c_char,
    vl: FfmpegVaList,
) {
    let state = INIT_STATE.load();
    let Some(s) = state.as_ref() else {
        return;
    };

    // FFmpeg only stores the threshold; filtering is the callback's job.
    if level > unsafe { ff::av_log_get_level() } {
        return;
    }

    let mut line = [0 as c_char; LOG_LINE_CAP as usize];
    let mut print_prefix: c_int = 1;
    unsafe {
        ff::av_log_format_line2(
            avcl,
            level,
            fmt,
            vl,
            line.as_mut_ptr(),
            LOG_LINE_CAP,
            &mut print_prefix,
        );
    }

    let callback = if level <= AV_LOG_ERROR {
        &s.error_callback
    } else if level <= AV_LOG_WARNING {
        &s.warning_callback
    } else {
        &s.log_callback
    };
    // av_log_format_line2 always NUL-terminates within the buffer.
    callback(line.as_ptr());
}

/// Sets the FFmpeg verbosity threshold (an `AV_LOG_*` constant). Messages above
/// this level are dropped before reaching any Unity sink.
#[unsafe(no_mangle)]
pub extern "C" fn uuav_set_log_level(level: c_int) {
    unsafe { ff::av_log_set_level(level) };
}

/// Initializes the global runtime.
///
/// `protocol_whitelist` is a NUL-terminated, comma-separated FFmpeg protocol
/// list; init fails if it is null or empty. Recommended baseline is
/// `https,http,tls,tcp,crypto,data,udp,rtp,rtcp,rtsp`, adding `file` only for
/// editor/local playback.
///
/// `warning_callback` and `log_callback` receive FFmpeg's own diagnostics
/// (already formatted, one line per call); `log_level` is the initial
/// `AV_LOG_*` verbosity threshold. All three callbacks must be non-null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_init(
    texture: *const c_void,
    audio_options: AudioOptionsRaw,
    error_callback: Option<RawErrorCallback>,
    warning_callback: Option<RawLogCallback>,
    log_callback: Option<RawLogCallback>,
    protocol_whitelist: *const c_char,
    log_level: c_int,
) -> ResultFFI {
    if INIT_STATE.load().is_some() {
        return ResultFFI::error("Already initialized");
    }

    let Some(error_callback) = error_callback else {
        return ResultFFI::error("Error callback is null");
    };

    let Some(warning_callback) = warning_callback else {
        return ResultFFI::error("Warning callback is null");
    };

    let Some(log_callback) = log_callback else {
        return ResultFFI::error("Log callback is null");
    };

    if texture.is_null() {
        return ResultFFI::error("Texture to capture the HwDevice from is not provided");
    }

    let device = match unsafe { hw_device::HwDevice::from_texture(texture) } {
        Ok(hw_device) => hw_device,
        Err(e) => return e.into(),
    };

    let audio_options = match AudioOptions::try_from(audio_options) {
        Ok(options) => options,
        Err(e) => return e.into(),
    };

    let protocol_whitelist = match unsafe { StreamingProtocol::new(protocol_whitelist) } {
        Ok(protocols) => protocols,
        Err(e) => return e.into(),
    };

    let new_runtime = Runtime {
        device,
        error_callback: Arc::new(error_callback),
        warning_callback: Arc::new(warning_callback),
        log_callback: Arc::new(log_callback),
        audio_options: Arc::new(ArcSwap::new(Arc::new(audio_options))),
        protocol_whitelist: Arc::new(protocol_whitelist),
        registry: DashMap::new(),
    };

    INIT_STATE.store(Some(Arc::new(new_runtime)));

    // Register the trampoline once; the sinks live in INIT_STATE, so it no-ops
    // after deinit and picks up fresh callbacks on re-init. The level store is
    // process-global, so re-apply it every init.
    LOG_INIT.call_once(|| unsafe { ff::av_log_set_callback(Some(uuav_ffmpeg_log)) });
    unsafe { ff::av_log_set_level(log_level) };

    ResultFFI::ok()
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_deinit() {
    INIT_STATE.store(None);
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_update_audio_out(options: AudioOptionsRaw) -> ResultFFI {
    let state = INIT_STATE.load();

    let Some(s) = state.as_ref() else {
        return ResultFFI::error("Not initialized");
    };

    match AudioOptions::try_from(options) {
        Ok(options) => {
            s.audio_options.store(Arc::new(options));
            ResultFFI::ok()
        }
        Err(e) => ResultFFI::error(&e.to_string()),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_status() -> Status {
    if let Some(s) = INIT_STATE.load().as_ref() {
        let audio_options = (**s.audio_options.load()).into();
        let players_count = s.registry.len() as u64;

        // D3D11 devices can be removed (driver reset, GPU hang)
        // Metal has no such concept
        #[cfg(target_os = "windows")]
        let device_remove_reason = match unsafe { s.device.device().GetDeviceRemovedReason() } {
            Ok(()) => ptr::null(),
            Err(e) => string_to_c_bytes(e.to_string()),
        };
        #[cfg(target_os = "macos")]
        let device_remove_reason = ptr::null();

        Status {
            players_count,
            initialized: true,
            audio_options,
            device_remove_reason,
        }
    } else {
        Status::default()
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_new() -> NewPlayerResult {
    let state = INIT_STATE.load();
    let Some(s) = state.as_ref() else {
        return NewPlayerResult::error(ERR_NO_RUNTIME);
    };

    let next_id = NEXT_STREAM_ID.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
    match UUAVPlayer::new(
        next_id,
        s.device.clone(),
        AudioOptionsView(Arc::clone(&s.audio_options)),
        ErrorCallback::from_raw(Arc::downgrade(&s.error_callback)),
        Arc::clone(&s.protocol_whitelist),
    ) {
        Ok(player) => {
            s.registry.insert(next_id, player);
            NewPlayerResult::ok(next_id)
        }
        Err(e) => NewPlayerResult::error(e.to_string()),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_free(player_id: PlayerId) {
    let state = INIT_STATE.load();
    let Some(s) = state.as_ref() else {
        return;
    };

    s.registry.remove(&player_id);
}

// ---- lifecycle -------------------------------------------------------

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_play(player_id: PlayerId) -> ResultFFI {
    uuav_player_play_internal(player_id).into()
}

fn uuav_player_play_internal(player_id: PlayerId) -> anyhow::Result<()> {
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    runtime.player_by_id(player_id)?.play()
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_pause(player_id: PlayerId) -> ResultFFI {
    uuav_player_pause_internal(player_id).into()
}

fn uuav_player_pause_internal(player_id: PlayerId) -> anyhow::Result<()> {
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    runtime.player_by_id(player_id)?.pause()
}

// async! returns immediately
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_open_media_async(
    player_id: PlayerId,
    url: *const c_char,
) -> ResultFFI {
    unsafe { uuav_player_open_media_async_internal(player_id, url) }.into()
}

unsafe fn uuav_player_open_media_async_internal(
    player_id: PlayerId,
    url: *const c_char,
) -> anyhow::Result<()> {
    ensure!(!url.is_null(), "url is null");
    let url = unsafe { CStr::from_ptr(url) }
        .to_str()
        .context("url is not valid UTF-8")?
        .to_owned();
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    runtime.player_by_id_mut(player_id)?.open_media_intent(url)
}

// back to CLOSED, player reusable
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_close_media(player_id: PlayerId) -> ResultFFI {
    uuav_player_close_media_internal(player_id).into()
}

fn uuav_player_close_media_internal(player_id: PlayerId) -> anyhow::Result<()> {
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    runtime.player_by_id_mut(player_id)?.close_media();
    Ok(())
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_state(player_id: PlayerId) -> UUAVState {
    let state = INIT_STATE.load();
    let Some(runtime) = state.as_ref() else {
        return UUAVState::UUAV_UNKNOWN;
    };

    runtime
        .player_by_id(player_id)
        .map_or(UUAVState::UUAV_UNKNOWN, |player| player.state())
}

// may be unavailable for realtime streams
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_duration(
    player_id: PlayerId,
    out_duration: *mut f64,
) -> ResultFFI {
    unsafe { uuav_player_duration_internal(player_id, out_duration) }.into()
}

unsafe fn uuav_player_duration_internal(
    player_id: PlayerId,
    out_duration: *mut f64,
) -> anyhow::Result<()> {
    ensure!(!out_duration.is_null(), "out pointer is null");
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    let duration = runtime
        .player_by_id(player_id)?
        .duration()
        .context("duration is not available")?;
    unsafe { out_duration.write(duration) };
    Ok(())
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_current_controls_state(
    player_id: PlayerId,
    out_state: *mut ControlsState,
) -> ResultFFI {
    unsafe { uuav_player_current_controls_state_internal(player_id, out_state) }.into()
}

unsafe fn uuav_player_current_controls_state_internal(
    player_id: PlayerId,
    out_state: *mut ControlsState,
) -> anyhow::Result<()> {
    ensure!(!out_state.is_null(), "out pointer is null");
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    let controls = runtime.player_by_id(player_id)?.controls_state();
    unsafe { out_state.write(controls) };
    Ok(())
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_current_time(
    player_id: PlayerId,
    out_time: *mut f64,
) -> ResultFFI {
    unsafe { uuav_player_current_time_internal(player_id, out_time) }.into()
}

unsafe fn uuav_player_current_time_internal(
    player_id: PlayerId,
    out_time: *mut f64,
) -> anyhow::Result<()> {
    ensure!(!out_time.is_null(), "out pointer is null");
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    let time = runtime
        .player_by_id(player_id)?
        .current_time()
        .context("current time is not available")?;
    unsafe { out_time.write(time) };
    Ok(())
}

// the player slaves its playback to the externally provided master clock
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_assign_master_clock(
    player_id: PlayerId,
    current_time: f64,
) -> ResultFFI {
    uuav_player_assign_master_clock_internal(player_id, current_time).into()
}

fn uuav_player_assign_master_clock_internal(
    player_id: PlayerId,
    current_time: f64,
) -> anyhow::Result<()> {
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    runtime
        .player_by_id(player_id)?
        .assign_master_clock(current_time)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_video_size(
    player_id: PlayerId,
    out_size: *mut VideoSize,
) -> ResultFFI {
    unsafe { uuav_player_get_video_size_internal(player_id, out_size) }.into()
}

unsafe fn uuav_player_get_video_size_internal(
    player_id: PlayerId,
    out_size: *mut VideoSize,
) -> anyhow::Result<()> {
    ensure!(!out_size.is_null(), "out pointer is null");
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    let size = runtime
        .player_by_id(player_id)?
        .video_size()
        .context("video size is not available yet")?;
    unsafe { out_size.write(size) };
    Ok(())
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_media_info(
    player_id: PlayerId,
    out_info: *mut MediaInfo,
) -> ResultFFI {
    unsafe { uuav_player_get_media_info_internal(player_id, out_info) }.into()
}

unsafe fn uuav_player_get_media_info_internal(
    player_id: PlayerId,
    out_info: *mut MediaInfo,
) -> anyhow::Result<()> {
    ensure!(!out_info.is_null(), "out pointer is null");
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    let info = runtime
        .player_by_id(player_id)?
        .media_info()
        .context("media info is not available yet")?;
    unsafe { out_info.write(info) };
    Ok(())
}

// ---- transport (commands: set flags, decoder thread obeys) ----------

// async; coalesces repeated calls
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_seek_async(player_id: PlayerId, time: f64) -> ResultFFI {
    uuav_player_seek_async_internal(player_id, time).into()
}

fn uuav_player_seek_async_internal(player_id: PlayerId, time: f64) -> anyhow::Result<()> {
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    runtime.player_by_id(player_id)?.seek_intent(time)
}

// persists across url switches
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_set_looping(player_id: PlayerId, looping: u8) -> ResultFFI {
    uuav_player_set_looping_internal(player_id, looping != 0).into()
}

fn uuav_player_set_looping_internal(player_id: PlayerId, looping: bool) -> anyhow::Result<()> {
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    runtime.player_by_id(player_id)?.set_looping(looping);
    Ok(())
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_get_looping(player_id: PlayerId) -> u8 {
    let state = INIT_STATE.load();
    let Some(runtime) = state.as_ref() else {
        return 0;
    };
    runtime
        .player_by_id(player_id)
        .map_or(0, |player| u8::from(player.looping()))
}

// Expect: realtime streams (no duration) keep playing at 1x
#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_set_rate(player_id: PlayerId, rate: f64) -> ResultFFI {
    uuav_player_set_rate_internal(player_id, rate).into()
}

fn uuav_player_set_rate_internal(player_id: PlayerId, rate: f64) -> anyhow::Result<()> {
    ensure!(
        rate.is_finite() && rate > 0.0,
        "playback rate must be finite and positive, got {rate}"
    );
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    runtime.player_by_id(player_id)?.set_rate(rate);
    Ok(())
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_get_rate(player_id: PlayerId) -> f64 {
    let state = INIT_STATE.load();
    let Some(runtime) = state.as_ref() else {
        return DEFAULT_PLAYBACK_RATE;
    };
    runtime
        .player_by_id(player_id)
        .map_or(DEFAULT_PLAYBACK_RATE, |player| player.rate())
}

// ---- video -----------------------------------------------------------

// Unity's UnityRenderingEvent signature
pub type UUAVRenderEvent = extern "C" fn(event_id: i32);

// [render] entry point issued via GL.IssuePluginEvent;
// `event_id` routes to the player with the matching id
extern "C" fn uuav_render_event(event_id: i32) {
    let Ok(player_id) = PlayerId::try_from(event_id) else {
        return;
    };

    let state = INIT_STATE.load();
    let Some(runtime) = state.as_ref() else {
        return;
    };

    if let Ok(player) = runtime.player_by_id(player_id) {
        player.on_render_event();
    }
}

// pass to GL.IssuePluginEvent
#[unsafe(no_mangle)]
pub extern "C" fn uuav_get_render_callback() -> UUAVRenderEvent {
    uuav_render_event
}

// Valid from the first presented frame; what the pointer is per platform
// and when it invalidates is documented on `video_output::VideoTextureView`.
// `plane` is part of the fixed C ABI; only Metal consumes it.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_video_texture(
    player_id: PlayerId,
    #[cfg_attr(target_os = "windows", allow(unused_variables))] plane: i32,
    out_texture: *mut *const c_void,
) -> ResultFFI {
    unsafe {
        uuav_player_get_video_texture_internal(
            player_id,
            #[cfg(target_os = "macos")]
            plane,
            out_texture,
        )
    }
    .into()
}

unsafe fn uuav_player_get_video_texture_internal(
    player_id: PlayerId,
    #[cfg(target_os = "macos")] plane: i32,
    out_texture: *mut *const c_void,
) -> anyhow::Result<()> {
    ensure!(!out_texture.is_null(), "out pointer is null");
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    let texture = runtime
        .player_by_id(player_id)?
        .video_texture(
            #[cfg(target_os = "macos")]
            plane,
        )
        .context("video texture is not available yet")?;
    unsafe { out_texture.write(texture.raw_ptr_mut().cast_const()) };
    Ok(())
}

// ---- audio -----------------------------------------------------------

// [audio] fills interleaved FLT; pads silence on underrun,
// never blocks; returns frames actually copied
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_read_audio(
    player_id: PlayerId,
    dst: *mut f32,
    nb_frames: i32,
) -> i32 {
    if dst.is_null() || nb_frames <= 0 {
        return 0;
    }

    let state = INIT_STATE.load();
    let Some(runtime) = state.as_ref() else {
        return 0;
    };

    runtime
        .player_by_id(player_id)
        .map_or(0, |player| player.read_audio(dst, nb_frames))
}

// [audio] like uuav_player_read_audio, additionally writing the media time
// of the first copied sample to out_pts (NaN while unknown)
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_read_audio_pts(
    player_id: PlayerId,
    dst: *mut f32,
    nb_frames: i32,
    out_pts: *mut f64,
) -> i32 {
    if dst.is_null() || nb_frames <= 0 {
        return 0;
    }
    if out_pts.is_null() {
        return unsafe { uuav_player_read_audio(player_id, dst, nb_frames) };
    }

    let state = INIT_STATE.load();
    let Some(runtime) = state.as_ref() else {
        unsafe { out_pts.write(f64::NAN) };
        return 0;
    };

    let mut pts = f64::NAN;
    let read = runtime
        .player_by_id(player_id)
        .map_or(0, |player| player.read_audio_pts(dst, nb_frames, &mut pts));
    unsafe { out_pts.write(pts) };
    read
}

// cumulative audio pipeline counters; accumulate across url switches
#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_audio_pipeline_stats(
    player_id: PlayerId,
    out_stats: *mut AudioPipelineStats,
) -> ResultFFI {
    unsafe { uuav_player_audio_pipeline_stats_internal(player_id, out_stats) }.into()
}

unsafe fn uuav_player_audio_pipeline_stats_internal(
    player_id: PlayerId,
    out_stats: *mut AudioPipelineStats,
) -> anyhow::Result<()> {
    ensure!(!out_stats.is_null(), "out pointer is null");
    let state = INIT_STATE.load();
    let runtime = state.as_ref().context(ERR_NO_RUNTIME)?;
    let snapshot = runtime.player_by_id(player_id)?.audio_pipeline_stats();
    unsafe { out_stats.write(snapshot) };
    Ok(())
}
