
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};
use std::ptr;
use std::sync::atomic::{AtomicBool, AtomicPtr, Ordering};

use anyhow::{Result, anyhow, bail};
use uuav_ipc::protocol::{LogLevel, MediaFactsValue, SharedSegment};

use crate::probe::Probe;

const AV_LOG_WARNING: c_int = 24;

const FRAME_INFO_UNSET: uuav_abi::FrameInfo = uuav_abi::FrameInfo {
    yuv_to_rgb: [0.0; 12],
    uv_transform: [0.0; 6],
    visible_width: 0,
    visible_height: 0,
    plane_width: [0; 2],
    plane_height: [0; 2],
    colorspace: 0,
    color_range: 0,
    color_primaries: 0,
    rotation: 0,
    bit_depth: 0,
    frame_index: 0,
    surface_generation: 0,
    planes: [0; 2],
};

pub struct Core {
    player: uuav::PlayerId,
    render: uuav::UUAVRenderEvent,
    render_event: i32,
    _probe: Probe,
}

impl Core {
    pub fn start(probe: Probe, engine: (u32, u32), whitelist: &str) -> Result<Self> {
        let whitelist = CString::new(whitelist)
            .map_err(|_| anyhow!("the protocol whitelist contains a NUL byte"))?;
        let audio = uuav::AudioOptionsRaw {
            sample_rate: i32::try_from(engine.0).unwrap_or(0),
            channels: i32::try_from(engine.1).unwrap_or(0),
        };

        let result = unsafe {
            uuav::uuav_init(
                probe.as_ptr(),
                audio,
                Some(error_sink),
                Some(warning_sink),
                Some(log_sink),
                whitelist.as_ptr(),
                AV_LOG_WARNING,
            )
        };
        into_result(&result).map_err(|error| anyhow!("uuav_init: {error}"))?;

        let created = uuav::uuav_player_new();
        if !created.error_message.is_null() {
            let message = take_string(created.error_message);
            uuav::uuav_deinit();
            bail!("uuav_player_new: {message}");
        }

        let Ok(render_event) = i32::try_from(created.player_id) else {
            uuav::uuav_player_free(created.player_id);
            uuav::uuav_deinit();
            bail!(
                "the core assigned player id {}, which does not fit the i32 render event id",
                created.player_id
            );
        };

        Ok(Self {
            player: created.player_id,
            render: uuav::uuav_get_render_callback(),
            render_event,
            _probe: probe,
        })
    }

    pub fn on_render_event(&self) {
        (self.render)(self.render_event);
    }

    pub fn open(&self, url: &str) -> Result<()> {
        let url = CString::new(url).map_err(|_| anyhow!("the media url contains a NUL byte"))?;
        let result = unsafe { uuav::uuav_player_open_media_async(self.player, url.as_ptr()) };
        into_result(&result).map_err(|error| anyhow!("{error}"))
    }

    pub fn play(&self) -> Result<()> {
        into_result(&uuav::uuav_player_play(self.player)).map_err(|error| anyhow!("{error}"))
    }

    pub fn pause(&self) -> Result<()> {
        into_result(&uuav::uuav_player_pause(self.player)).map_err(|error| anyhow!("{error}"))
    }

    pub fn close(&self) -> Result<()> {
        into_result(&uuav::uuav_player_close_media(self.player)).map_err(|error| anyhow!("{error}"))
    }

    pub fn set_log_level(level: c_int) {
        uuav::uuav_set_log_level(level);
    }

    pub fn seek(&self, seconds: f64) -> Result<()> {
        into_result(&uuav::uuav_player_seek_async(self.player, seconds))
            .map_err(|error| anyhow!("{error}"))
    }

    pub fn set_looping(&self, looping: bool) -> Result<()> {
        into_result(&uuav::uuav_player_set_looping(self.player, u8::from(looping)))
            .map_err(|error| anyhow!("{error}"))
    }

    pub fn set_rate(&self, rate: f64) -> Result<()> {
        into_result(&uuav::uuav_player_set_rate(self.player, rate))
            .map_err(|error| anyhow!("{error}"))
    }


    pub fn update_audio_out(sample_rate: u32, channels: u32) -> Result<()> {
        let options = uuav::AudioOptionsRaw {
            sample_rate: i32::try_from(sample_rate).unwrap_or(0),
            channels: i32::try_from(channels).unwrap_or(0),
        };
        into_result(&uuav::uuav_update_audio_out(options)).map_err(|error| anyhow!("{error}"))
    }

    pub fn state(&self) -> u32 {
        uuav::uuav_player_state(self.player) as u32
    }

    pub fn rate(&self) -> f64 {
        uuav::uuav_player_get_rate(self.player)
    }

    pub fn current_time(&self) -> Option<f64> {
        let mut seconds = 0.0_f64;
        let result = unsafe { uuav::uuav_player_current_time(self.player, &raw mut seconds) };
        into_result(&result).ok().map(|()| seconds)
    }

    pub fn duration(&self) -> Option<f64> {
        let mut seconds = 0.0_f64;
        let result = unsafe { uuav::uuav_player_duration(self.player, &raw mut seconds) };
        into_result(&result).ok().map(|()| seconds)
    }

    pub fn video_size(&self) -> Option<(u32, u32)> {
        let mut size = uuav::VideoSize {
            width: 0,
            height: 0,
        };
        let result = unsafe { uuav::uuav_player_get_video_size(self.player, &raw mut size) };
        into_result(&result).ok().map(|()| (size.width, size.height))
    }

    pub fn frame_info(&self) -> Option<uuav_abi::FrameInfo> {
        let mut info = FRAME_INFO_UNSET;
        let result = unsafe {
            uuav::uuav_player_get_frame_info(self.player, ptr::from_mut(&mut info).cast())
        };
        into_result(&result).ok().map(|()| info)
    }

    pub fn controls_state(&self) -> Option<uuav::ControlsState> {
        let mut state = uuav::ControlsState {
            rate: 1.0,
            play: 0,
            play_pending: 0,
            looping: 0,
            looping_pending: 0,
            rate_pending: 0,
        };
        let result =
            unsafe { uuav::uuav_player_current_controls_state(self.player, &raw mut state) };
        into_result(&result).ok().map(|()| state)
    }

    pub fn facts(&self, open_generation: u64) -> Option<MediaFactsValue> {
        let mut info = uuav_abi::MediaInfo::empty();
        let result = unsafe {
            uuav::uuav_player_get_media_info(self.player, ptr::from_mut(&mut info).cast())
        };
        into_result(&result).ok()?;

        let (width, height) = self.video_size().unwrap_or((0, 0));
        Some(MediaFactsValue {
            open_generation,
            duration: self.duration().filter(|value| *value > 0.0).unwrap_or(0.0),
            visible_width: width,
            visible_height: height,
            has_video: info.has_video != 0,
            has_audio: info.has_audio != 0,
            sample_rate: u32::try_from(info.sample_rate).unwrap_or(0),
            channels: u32::try_from(info.channels).unwrap_or(0),
        })
    }

    pub fn stream_summary(&self) -> Option<String> {
        let mut info = uuav_abi::MediaInfo::empty();
        let result = unsafe {
            uuav::uuav_player_get_media_info(self.player, ptr::from_mut(&mut info).cast())
        };
        into_result(&result).ok()?;
        if info.has_video == 0 {
            return None;
        }
        Some(format!(
            "uuav-core: stream {}x{} @ {:.3} fps codec={} pixfmt={} vbitrate={}",
            info.width,
            info.height,
            info.framerate,
            c_name(&info.video_codec),
            c_name(&info.pixel_format),
            info.video_bitrate,
        ))
    }
}

fn c_name(bytes: &[c_char]) -> String {
    unsafe { CStr::from_ptr(bytes.as_ptr()) }
        .to_string_lossy()
        .into_owned()
}

impl Drop for Core {
    fn drop(&mut self) {
        uuav::uuav_player_free(self.player);
        uuav::uuav_deinit();
    }
}

fn into_result(result: &uuav::ResultFFI) -> Result<(), String> {
    if result.error_message.is_null() {
        return Ok(());
    }
    Err(take_string(result.error_message))
}

fn take_string(message: *const c_char) -> String {
    let text = unsafe { CStr::from_ptr(message) }
        .to_string_lossy()
        .into_owned();
    unsafe { uuav::uuav_string_free(message.cast_mut()) };
    text
}

static LOG_SEGMENT: AtomicPtr<SharedSegment> = AtomicPtr::new(ptr::null_mut());
static LOG_BUSY: AtomicBool = AtomicBool::new(false);

const LOG_DRAIN_SPINS: u32 = 1_000_000;

pub struct LogBridge;

impl LogBridge {
    pub fn install(segment: &SharedSegment) -> Self {
        LOG_SEGMENT.store(ptr::from_ref(segment).cast_mut(), Ordering::Release);
        Self
    }
}

impl Drop for LogBridge {
    fn drop(&mut self) {
        for _ in 0..LOG_DRAIN_SPINS {
            if !LOG_BUSY.load(Ordering::Acquire) {
                break;
            }
            std::hint::spin_loop();
        }
        LOG_SEGMENT.store(ptr::null_mut(), Ordering::Release);
    }
}

extern "C" fn error_sink(line: *const c_char) {
    emit(LogLevel::Error, line);
}

extern "C" fn warning_sink(line: *const c_char) {
    emit(LogLevel::Warning, line);
}

extern "C" fn log_sink(line: *const c_char) {
    emit(LogLevel::Info, line);
}

fn emit(level: LogLevel, line: *const c_char) {
    if line.is_null() {
        return;
    }
    if LOG_BUSY
        .compare_exchange(false, true, Ordering::Acquire, Ordering::Relaxed)
        .is_err()
    {
        return;
    }
    let segment = LOG_SEGMENT.load(Ordering::Acquire);
    if !segment.is_null() {
        let segment = unsafe { &*segment };
        let text = unsafe { CStr::from_ptr(line) }.to_string_lossy();
        segment.log.emit(level, text.trim_end());
    }
    LOG_BUSY.store(false, Ordering::Release);
}
