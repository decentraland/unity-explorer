
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

pub mod audio_ring;
pub mod device;

use std::ffi::{CStr, CString};
use std::num::NonZeroI32;
use std::os::raw::{c_char, c_int, c_void};
use std::path::PathBuf;
use std::ptr;
use std::sync::Arc;
use std::sync::atomic::{AtomicI32, AtomicU64, Ordering};

use arc_swap::{ArcSwap, ArcSwapOption};
use uuav_abi::errors;

use crate::device::EngineDevice;

pub use uuav_abi::{
    AudioOptionsRaw, AudioSyncFFI, ControlsState, FrameInfo, MEDIA_INFO_NAME_LEN, MediaInfo,
    NewPlayerResult, PlayerId, RawLogCallback, ResultFFI, Status, UUAVRenderEvent, UUAVState,
    VideoSize,
};

#[cfg(target_os = "macos")]
use uuav_ipc::registry::{self as sessions, LogSinks, Player, PlayerConfig, Registry};
#[cfg(windows)]
use uuav_ipc::win::registry::{self as sessions, LogSinks, Player, PlayerConfig, Registry};

static INIT_STATE: ArcSwapOption<Runtime> = ArcSwapOption::const_empty();
static NEXT_ID: AtomicU64 = AtomicU64::new(1);

const MAX_PLAYER_ID: PlayerId = i32::MAX as PlayerId;

#[derive(Clone, Copy, PartialEq, Eq)]
struct AudioOptions {
    sample_rate: NonZeroI32,
    channels: NonZeroI32,
}

impl TryFrom<AudioOptionsRaw> for AudioOptions {
    type Error = String;

    fn try_from(raw: AudioOptionsRaw) -> Result<Self, Self::Error> {
        let sample_rate = NonZeroI32::new(raw.sample_rate)
            .filter(|value| value.get() > 0)
            .ok_or_else(|| format!("sample_rate must be positive, got {}", raw.sample_rate))?;
        let channels = NonZeroI32::new(raw.channels)
            .filter(|value| value.get() > 0)
            .ok_or_else(|| format!("channels must be positive, got {}", raw.channels))?;
        Ok(Self {
            sample_rate,
            channels,
        })
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

impl AudioOptions {
    fn as_engine(self) -> (u32, u32) {
        (
            u32::try_from(self.sample_rate.get()).unwrap_or(0),
            u32::try_from(self.channels.get()).unwrap_or(0),
        )
    }
}

struct Runtime {
    engine: ArcSwap<AudioOptions>,
    protocol_whitelist: String,
    adapter_exe: PathBuf,
    device: EngineDevice,
    sinks: LogSinks,
    registry: Registry,
    log_level: AtomicI32,
}

fn runtime() -> Option<Arc<Runtime>> {
    INIT_STATE.load_full()
}

fn string_to_c(message: &str) -> *const c_char {
    CString::new(message).unwrap_or_default().into_raw()
}

fn error_result(message: &str) -> ResultFFI {
    ResultFFI {
        error_message: string_to_c(message),
    }
}

fn result_ffi(value: Result<(), String>) -> ResultFFI {
    match value {
        Ok(()) => ResultFFI::ok(),
        Err(message) => error_result(&message),
    }
}

fn new_player_error(message: &str) -> NewPlayerResult {
    NewPlayerResult {
        player_id: 0,
        error_message: string_to_c(message),
    }
}

fn with_player<R>(player_id: PlayerId, f: impl FnOnce(&Player) -> R) -> Result<R, String> {
    let runtime = runtime().ok_or_else(|| errors::NO_RUNTIME.to_owned())?;
    runtime
        .registry
        .with(player_id, f)
        .ok_or_else(|| errors::NO_PLAYER.to_owned())
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_string_free(string: *mut c_char) {
    if string.is_null() {
        return;
    }
    drop(unsafe { CString::from_raw(string) });
}

#[unsafe(no_mangle)]
pub const extern "C" fn uuav_abi_version() -> *const c_char {
    uuav_abi::ABI_VERSION.as_ptr()
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_set_log_level(level: c_int) {
    if let Some(runtime) = runtime() {
        runtime.log_level.store(level, Ordering::Relaxed);
        runtime
            .registry
            .for_each(|player| player.set_log_level(level));
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_set_fetch_provider(provider: Option<uuav_ipc::fetch::FetchProvider>) {
    uuav_ipc::fetch::set_provider(provider);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_init(
    texture: *const c_void,
    audio_options: AudioOptionsRaw,
    error_callback: Option<RawLogCallback>,
    warning_callback: Option<RawLogCallback>,
    log_callback: Option<RawLogCallback>,
    protocol_whitelist: *const c_char,
    log_level: c_int,
) -> ResultFFI {
    if INIT_STATE.load().is_some() {
        return error_result(errors::ALREADY_INITIALIZED);
    }
    let Some(error) = error_callback else {
        return error_result(errors::ERROR_CALLBACK_NULL);
    };
    let Some(warning) = warning_callback else {
        return error_result(errors::WARNING_CALLBACK_NULL);
    };
    let Some(log) = log_callback else {
        return error_result(errors::LOG_CALLBACK_NULL);
    };
    if texture.is_null() {
        return error_result(errors::NO_TEXTURE);
    }
    let device = match unsafe { EngineDevice::from_probe(texture) } {
        Ok(device) => device,
        Err(message) => return error_result(&message),
    };
    let engine = match AudioOptions::try_from(audio_options) {
        Ok(engine) => engine,
        Err(message) => return error_result(&message),
    };
    if protocol_whitelist.is_null() {
        return error_result(errors::PROTOCOL_WHITELIST_NULL);
    }
    let whitelist = match unsafe { CStr::from_ptr(protocol_whitelist) }.to_str() {
        Ok(text) if !text.is_empty() => text.to_owned(),
        Ok(_) => return error_result(errors::PROTOCOL_WHITELIST_EMPTY),
        Err(_) => return error_result("protocol_whitelist is not valid UTF-8"),
    };
    let adapter_exe = match sessions::resolve_adapter() {
        Ok(path) => path,
        Err(message) => return error_result(&message),
    };

    let runtime = Runtime {
        engine: ArcSwap::from_pointee(engine),
        protocol_whitelist: whitelist,
        adapter_exe,
        device,
        sinks: LogSinks {
            error,
            warning,
            log,
        },
        registry: Registry::new(),
        log_level: AtomicI32::new(log_level),
    };
    INIT_STATE.store(Some(Arc::new(runtime)));
    ResultFFI::ok()
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_deinit() {
    INIT_STATE.store(None);
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_update_audio_out(options: AudioOptionsRaw) -> ResultFFI {
    let Some(runtime) = runtime() else {
        return error_result(errors::NOT_INITIALIZED);
    };
    let engine = match AudioOptions::try_from(options) {
        Ok(engine) => engine,
        Err(message) => return error_result(&message),
    };
    runtime.engine.store(Arc::new(engine));
    let (sample_rate, channels) = engine.as_engine();
    runtime
        .registry
        .for_each(|player| player.update_audio_out(sample_rate, channels));
    ResultFFI::ok()
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_status() -> Status {
    match runtime() {
        Some(runtime) => Status {
            players_count: runtime.registry.len() as u64,
            initialized: true,
            audio_options: (**runtime.engine.load()).into(),
            device_remove_reason: runtime
                .device
                .removed_reason()
                .map_or_else(ptr::null, |reason| string_to_c(&reason)),
        },
        None => Status::default(),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_new() -> NewPlayerResult {
    let Some(runtime) = runtime() else {
        return new_player_error(errors::NO_RUNTIME);
    };
    let id = NEXT_ID.fetch_add(1, Ordering::Relaxed);
    if id > MAX_PLAYER_ID {
        return new_player_error("the player id space is exhausted");
    }
    let (sample_rate, channels) = (**runtime.engine.load()).as_engine();
    let config = PlayerConfig {
        protocol_whitelist: runtime.protocol_whitelist.clone(),
        engine_sample_rate: sample_rate,
        engine_channels: channels,
        sinks: runtime.sinks,
        hello_timeout_ms: sessions::hello_timeout_ms(),
        log_level: runtime.log_level.load(Ordering::Relaxed),
        #[cfg(windows)]
        device: runtime.device.d3d11(),
    };
    match runtime.registry.create(id, &runtime.adapter_exe, config) {
        Ok(()) => NewPlayerResult::ok(id),
        Err(message) => new_player_error(&message),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_free(player_id: PlayerId) {
    if let Some(runtime) = runtime() {
        runtime.registry.remove(player_id);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_play(player_id: PlayerId) -> ResultFFI {
    result_ffi(with_player(player_id, Player::play))
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_pause(player_id: PlayerId) -> ResultFFI {
    result_ffi(with_player(player_id, Player::pause))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_open_media_async(
    player_id: PlayerId,
    url: *const c_char,
) -> ResultFFI {
    if url.is_null() {
        return error_result(errors::URL_NULL);
    }
    let url = match unsafe { CStr::from_ptr(url) }.to_str() {
        Ok(text) => text.to_owned(),
        Err(_) => return error_result(errors::URL_NOT_UTF8),
    };
    result_ffi(with_player(player_id, move |player| player.open_media(url)))
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_close_media(player_id: PlayerId) -> ResultFFI {
    result_ffi(with_player(player_id, Player::close_media))
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_state(player_id: PlayerId) -> UUAVState {
    with_player(player_id, |player| {
        UUAVState::from_code(player.uuav_state())
    })
    .unwrap_or(UUAVState::UUAV_UNKNOWN)
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_get_last_error(player_id: PlayerId) -> u64 {
    with_player(player_id, Player::last_error).unwrap_or(0)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_duration(
    player_id: PlayerId,
    out_duration: *mut f64,
) -> ResultFFI {
    if out_duration.is_null() {
        return error_result(errors::OUT_POINTER_NULL);
    }
    let result = with_player(player_id, Player::duration).and_then(|duration| {
        let duration = duration.ok_or_else(|| "duration is not available".to_owned())?;
        unsafe { out_duration.write(duration) };
        Ok(())
    });
    result_ffi(result)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_current_controls_state(
    player_id: PlayerId,
    out_state: *mut ControlsState,
) -> ResultFFI {
    if out_state.is_null() {
        return error_result(errors::OUT_POINTER_NULL);
    }
    let result = with_player(player_id, Player::controls_state).map(|state| {
        unsafe { out_state.write(state) };
    });
    result_ffi(result)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_current_time(
    player_id: PlayerId,
    out_time: *mut f64,
) -> ResultFFI {
    if out_time.is_null() {
        return error_result(errors::OUT_POINTER_NULL);
    }
    let result = with_player(player_id, Player::current_time).and_then(|time| {
        let time = time.ok_or_else(|| "current time is not available".to_owned())?;
        unsafe { out_time.write(time) };
        Ok(())
    });
    result_ffi(result)
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_assign_master_clock(
    player_id: PlayerId,
    current_time: f64,
) -> ResultFFI {
    result_ffi(with_player(player_id, move |player| {
        player.assign_master_clock(current_time);
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_video_size(
    player_id: PlayerId,
    out_size: *mut VideoSize,
) -> ResultFFI {
    if out_size.is_null() {
        return error_result(errors::OUT_POINTER_NULL);
    }
    let result = with_player(player_id, Player::video_size).and_then(|size| {
        let (width, height) = size.ok_or_else(|| "video size is not available yet".to_owned())?;
        unsafe { out_size.write(VideoSize { width, height }) };
        Ok(())
    });
    result_ffi(result)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_frame_info(
    player_id: PlayerId,
    out_info: *mut FrameInfo,
) -> ResultFFI {
    if out_info.is_null() {
        return error_result(errors::OUT_POINTER_NULL);
    }
    let result = with_player(player_id, Player::frame_info).and_then(|info| {
        let info = info.ok_or_else(|| "no frame has been presented yet".to_owned())?;
        unsafe { out_info.write(info) };
        Ok(())
    });
    result_ffi(result)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_media_info(
    player_id: PlayerId,
    out_info: *mut MediaInfo,
) -> ResultFFI {
    if out_info.is_null() {
        return error_result(errors::OUT_POINTER_NULL);
    }
    let result = with_player(player_id, Player::media_facts).and_then(|facts| {
        let facts = facts.ok_or_else(|| "media info is not available yet".to_owned())?;
        let mut info = MediaInfo::empty();
        info.duration = if facts.duration > 0.0 {
            facts.duration
        } else {
            -1.0
        };
        info.width = facts.visible_width;
        info.height = facts.visible_height;
        info.sample_rate = i32::try_from(facts.sample_rate).unwrap_or(0);
        info.channels = i32::try_from(facts.channels).unwrap_or(0);
        info.has_video = u8::from(facts.has_video);
        info.has_audio = u8::from(facts.has_audio);
        unsafe { out_info.write(info) };
        Ok(())
    });
    result_ffi(result)
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_seek_async(player_id: PlayerId, time: f64) -> ResultFFI {
    result_ffi(with_player(player_id, move |player| player.seek(time)))
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_set_looping(player_id: PlayerId, looping: u8) -> ResultFFI {
    result_ffi(with_player(player_id, move |player| {
        player.set_looping(looping != 0);
    }))
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_get_looping(player_id: PlayerId) -> u8 {
    with_player(player_id, |player| u8::from(player.looping())).unwrap_or(0)
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_set_rate(player_id: PlayerId, rate: f64) -> ResultFFI {
    if !(rate.is_finite() && rate > 0.0) {
        return error_result(&format!(
            "playback rate must be finite and positive, got {rate}"
        ));
    }
    result_ffi(with_player(player_id, move |player| player.set_rate(rate)))
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_player_get_rate(player_id: PlayerId) -> f64 {
    with_player(player_id, Player::rate).unwrap_or(1.0)
}

extern "C" fn uuav_render_event(event_id: i32) {
    let Ok(player_id) = PlayerId::try_from(event_id) else {
        return;
    };
    let _ = with_player(player_id, Player::on_render_event);
}

#[unsafe(no_mangle)]
pub extern "C" fn uuav_get_render_callback() -> UUAVRenderEvent {
    uuav_render_event
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_get_video_texture(
    player_id: PlayerId,
    plane: i32,
    out_texture: *mut *const c_void,
) -> ResultFFI {
    if out_texture.is_null() {
        return error_result(errors::OUT_POINTER_NULL);
    }
    let result =
        with_player(player_id, move |player| player.video_texture(plane)).and_then(|texture| {
            let texture = texture.ok_or_else(|| "video texture is not available yet".to_owned())?;
            unsafe { out_texture.write(texture) };
            Ok(())
        });
    result_ffi(result)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_audio_sync(
    player_id: PlayerId,
    out_sync: *mut AudioSyncFFI,
) -> u8 {
    if out_sync.is_null() {
        return 0;
    }
    let Some(ring) = uuav_ipc::audio::lookup(player_id) else {
        return 0;
    };
    let generation = ring.generation();
    let frames_consumed = ring.drained_frames();
    let (rate, state_sample_rate) = ring
        .state()
        .map_or((1.0, 0), |state| (state.clock.rate, state.sample_rate));
    let basis = ring.basis();
    let current = basis.filter(|basis| basis.generation == generation);
    let sync = AudioSyncFFI {
        media_time: current.map_or(0.0, |basis| {
            uuav_ipc::audio::media_time_of(&basis, frames_consumed, rate)
        }),
        base_pts: basis.map_or(0.0, |basis| basis.pts),
        rate,
        generation,
        frames_consumed,
        base_frames: basis.map_or(0, |basis| basis.frames_consumed),
        silence_calls: *ring.counters().get(5).unwrap_or(&0),
        sample_rate: basis.map_or(state_sample_rate, |basis| basis.sample_rate),
        priming: u32::from(!ring.prime_gate_is_open(generation)),
        has_basis: u32::from(current.is_some()),
        reserved: 0,
    };
    unsafe { out_sync.write(sync) };
    1
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_set_presentation_clock(
    player_id: PlayerId,
    media_time: f64,
) -> u8 {
    let Some(ring) = uuav_ipc::audio::lookup(player_id) else {
        return 0;
    };
    ring.set_presentation_clock(media_time);
    1
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn uuav_player_read_audio(
    player_id: PlayerId,
    dst: *mut f32,
    nb_frames: i32,
) -> i32 {
    if dst.is_null() || nb_frames <= 0 {
        return 0;
    }
    let Ok(frames) = usize::try_from(nb_frames) else {
        return 0;
    };
    unsafe { audio_ring::read(player_id, dst, frames) }
}
