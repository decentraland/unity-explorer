
use anyhow::{Result, anyhow, ensure};
use ffmpeg_sys_next as ff;
use std::ffi::{CStr, CString};
use std::num::NonZeroI32;
use std::os::raw::{c_char, c_int};

pub(crate) const AVERROR_EAGAIN: c_int = ff::AVERROR(libc::EAGAIN);

pub(crate) enum Decoded<T> {
    Frame(T),
    Again,
    Eof,
}

pub(crate) fn av_err(context: &str, code: c_int) -> anyhow::Error {
    let mut buf = [0 as c_char; 64];
    let message = unsafe {
        if ff::av_strerror(code, buf.as_mut_ptr(), buf.len()) == 0 {
            CStr::from_ptr(buf.as_ptr()).to_string_lossy().into_owned()
        } else {
            format!("ffmpeg error {code}")
        }
    };
    anyhow!("{context}: {message}")
}

pub(crate) fn check(context: &str, code: c_int) -> Result<c_int> {
    if code < 0 {
        Err(av_err(context, code))
    } else {
        Ok(code)
    }
}

pub(crate) const fn q2d(r: ff::AVRational) -> f64 {
    if r.den == 0 {
        0.0
    } else {
        r.num as f64 / r.den as f64
    }
}

pub(crate) fn copy_c_name<const N: usize>(dst: &mut [c_char; N], src: *const c_char) {
    let bytes: &[u8] = if src.is_null() {
        &[]
    } else {
        unsafe { CStr::from_ptr(src) }.to_bytes()
    };

    let len = bytes.len().min(N.saturating_sub(1));
    for (d, &s) in dst.iter_mut().zip(bytes.iter().take(len)) {
        *d = c_char::from_ne_bytes([s]);
    }

    if let Some(terminator) = dst.get_mut(len) {
        *terminator = 0;
    }
}

pub(crate) struct StreamingProtocol(CString);

impl StreamingProtocol {
    pub(crate) unsafe fn new(raw: *const c_char) -> Result<Self> {
        ensure!(!raw.is_null(), "protocol_whitelist is null");
        let value = unsafe { CStr::from_ptr(raw) };
        ensure!(!value.to_bytes().is_empty(), "protocol_whitelist is empty");
        Ok(Self(value.to_owned()))
    }
}

pub(crate) const FORMAT_WHITELIST: &CStr =
    c"mov,mp4,matroska,webm,hls,dash,mpegts,mp3,wav,ogg,flac,aac";

pub(crate) const CODEC_WHITELIST: &CStr =
    c"h264,hevc,vp9,av1,aac,mp3,mp3float,opus,vorbis,flac,pcm_s16le,pcm_s16be,pcm_f32le";

pub(crate) const HLS_ALLOWED_EXTENSIONS: &CStr = c"3gp,aac,avi,ac3,eac3,flac,mkv,m3u8,m4a,m4s,m4v,mpg,mov,mp2,mp3,mp4,mpeg,mpegts,ogg,ogv,oga,ts,vob,vtt,wav,webm,webvtt,cmfv,cmfa,ec3,fmp4,key";

const READ_TIMEOUT_MICROSECONDS: &CStr = c"15000000";

const HTTP_PERSISTENT: &CStr = c"0";

const RECONNECT_DELAY_MAX_SECONDS: &CStr = c"4";

const MAX_DECODED_PIXELS: i64 = 33_177_600;

const DECODE_THREAD_COUNT: c_int = 2;

pub(crate) unsafe fn apply_decode_limits(ctx: *mut ff::AVCodecContext) {
    unsafe {
        (*ctx).max_pixels = MAX_DECODED_PIXELS;
        (*ctx).thread_count = DECODE_THREAD_COUNT;
    }
}

fn codec_allowed(name: &str) -> bool {
    CODEC_WHITELIST
        .to_bytes()
        .split(|&byte| byte == b',')
        .any(|allowed| allowed == name.as_bytes())
}

pub(crate) struct AvDict(*mut ff::AVDictionary);

const GATE_KEYS: [&CStr; 3] = [
    c"protocol_whitelist",
    c"format_whitelist",
    c"codec_whitelist",
];

impl AvDict {
    pub(crate) fn open_options(protocol: &StreamingProtocol) -> Self {
        let mut dict: *mut ff::AVDictionary = std::ptr::null_mut();

        for (key, value) in [
            (c"protocol_whitelist", protocol.0.as_c_str()),
            (c"format_whitelist", FORMAT_WHITELIST),
            (c"codec_whitelist", CODEC_WHITELIST),
            (c"allowed_extensions", HLS_ALLOWED_EXTENSIONS),
            (c"rw_timeout", READ_TIMEOUT_MICROSECONDS),
            (c"http_persistent", HTTP_PERSISTENT),
            (c"reconnect", c"1"),
            (c"reconnect_streamed", c"1"),
            (c"reconnect_delay_max", RECONNECT_DELAY_MAX_SECONDS),
        ] {
            unsafe { ff::av_dict_set(&mut dict, key.as_ptr(), value.as_ptr(), 0) };
        }

        Self(dict)
    }

    #[allow(clippy::needless_pass_by_ref_mut)]
    pub(crate) const fn as_mut_ptr(&mut self) -> *mut *mut ff::AVDictionary {
        &raw mut self.0
    }

    pub(crate) fn ensure_gates_applied(&self) -> Result<()> {
        for key in GATE_KEYS {
            let entry = unsafe { ff::av_dict_get(self.0, key.as_ptr(), std::ptr::null(), 0) };
            ensure!(
                entry.is_null(),
                "FFmpeg ignored the {} option, so the media was opened ungated",
                key.to_string_lossy()
            );
        }
        Ok(())
    }
}

impl Drop for AvDict {
    fn drop(&mut self) {
        unsafe { ff::av_dict_free(&mut self.0) };
    }
}

#[derive(Clone, Copy)]
pub(crate) struct Stream(*mut ff::AVStream);

impl Stream {
    pub(crate) const unsafe fn from_raw(ptr: *mut ff::AVStream) -> Self {
        Self(ptr)
    }

    pub(crate) fn codecpar(self) -> *const ff::AVCodecParameters {
        unsafe { (*self.0).codecpar }
    }

    pub(crate) fn codec_id(self) -> ff::AVCodecID {
        unsafe { (*self.codecpar()).codec_id }
    }

    pub(crate) fn time_base(self) -> ff::AVRational {
        unsafe { (*self.0).time_base }
    }

    pub(crate) fn avg_frame_rate(self) -> ff::AVRational {
        unsafe { (*self.0).avg_frame_rate }
    }

    pub(crate) fn find_decoder(self) -> Result<*const ff::AVCodec> {
        let name =
            unsafe { CStr::from_ptr(ff::avcodec_get_name(self.codec_id())) }.to_string_lossy();
        ensure!(
            codec_allowed(&name),
            "codec {name} is not in the allowed set ({})",
            CODEC_WHITELIST.to_string_lossy()
        );

        let codec = unsafe { ff::avcodec_find_decoder(self.codec_id()) };
        if codec.is_null() {
            return Err(anyhow!("no decoder for codec id {:?}", self.codec_id()));
        }
        Ok(codec)
    }
}

pub(crate) struct OwnedDecoder(*mut ff::AVCodecContext);

impl OwnedDecoder {
    pub(crate) fn new(codec: *const ff::AVCodec) -> Result<Self> {
        let ptr = unsafe { ff::avcodec_alloc_context3(codec) };
        if ptr.is_null() {
            return Err(anyhow!("avcodec_alloc_context3 failed"));
        }
        Ok(Self(ptr))
    }

    #[allow(clippy::needless_pass_by_ref_mut)]
    pub(crate) const fn as_mut_ptr(&mut self) -> *mut ff::AVCodecContext {
        self.0
    }
}

impl Drop for OwnedDecoder {
    fn drop(&mut self) {
        unsafe { ff::avcodec_free_context(&mut self.0) };
    }
}

pub(crate) struct OwnedFrame(*mut ff::AVFrame);

unsafe impl Send for OwnedFrame {}

impl OwnedFrame {
    pub(crate) fn new() -> Result<Self> {
        let ptr = unsafe { ff::av_frame_alloc() };
        if ptr.is_null() {
            return Err(anyhow!("av_frame_alloc failed"));
        }
        Ok(Self(ptr))
    }

    #[allow(clippy::needless_pass_by_ref_mut)]
    pub(crate) const fn as_mut_ptr(&mut self) -> *mut ff::AVFrame {
        self.0
    }

    pub(crate) fn format(&self) -> c_int {
        unsafe { (*self.0).format }
    }

    pub(crate) fn width(&self) -> c_int {
        unsafe { (*self.0).width }
    }

    pub(crate) fn height(&self) -> c_int {
        unsafe { (*self.0).height }
    }

    pub(crate) fn best_effort_timestamp(&self) -> i64 {
        unsafe { (*self.0).best_effort_timestamp }
    }

    pub(crate) fn colorspace(&self) -> ff::AVColorSpace {
        unsafe { (*self.0).colorspace }
    }

    pub(crate) fn color_range(&self) -> ff::AVColorRange {
        unsafe { (*self.0).color_range }
    }

    pub(crate) fn color_primaries(&self) -> ff::AVColorPrimaries {
        unsafe { (*self.0).color_primaries }
    }

    pub(crate) fn display_rotation(&self) -> i32 {
        let side = unsafe {
            ff::av_frame_get_side_data(
                self.0,
                ff::AVFrameSideDataType::AV_FRAME_DATA_DISPLAYMATRIX,
            )
        };
        if side.is_null() || unsafe { (*side).size } < size_of::<[i32; 9]>() {
            return 0;
        }
        let degrees = unsafe { ff::av_display_rotation_get((*side).data.cast::<i32>()) };
        if !degrees.is_finite() {
            return 0;
        }
        ((-degrees / 90.0).round() as i32)
            .rem_euclid(4)
            .saturating_mul(90)
    }

    pub(crate) fn sample_rate(&self) -> c_int {
        unsafe { (*self.0).sample_rate }
    }

    pub(crate) fn nb_samples(&self) -> c_int {
        unsafe { (*self.0).nb_samples }
    }

    pub(crate) fn ch_layout(&self) -> &ff::AVChannelLayout {
        unsafe { &(*self.0).ch_layout }
    }

    pub(crate) fn extended_data(&self) -> *const *const u8 {
        unsafe { (*self.0).extended_data.cast::<*const u8>().cast_const() }
    }

    pub(crate) fn data(&self, plane: usize) -> *mut u8 {
        match unsafe { (*self.0).data.get(plane) } {
            Some(&pointer) => pointer,
            None => std::ptr::null_mut(),
        }
    }

    pub(crate) fn linesize(&self, plane: usize) -> c_int {
        match unsafe { (*self.0).linesize.get(plane) } {
            Some(&stride) => stride,
            None => 0,
        }
    }

    pub(crate) fn hw_frames_ctx(&self) -> Result<&ff::AVHWFramesContext> {
        unsafe {
            let buffer = (*self.0).hw_frames_ctx;
            if buffer.is_null() {
                return Err(anyhow!("frame has no hw frames context"));
            }
            Ok(&*(*buffer).data.cast::<ff::AVHWFramesContext>())
        }
    }
}

impl Drop for OwnedFrame {
    fn drop(&mut self) {
        unsafe { ff::av_frame_free(&mut self.0) };
    }
}

pub(crate) struct OwnedChannelLayout(ff::AVChannelLayout);

impl OwnedChannelLayout {
    pub(crate) fn default_for(channels: NonZeroI32) -> Self {
        let mut layout: ff::AVChannelLayout = unsafe { std::mem::zeroed() };
        unsafe { ff::av_channel_layout_default(&mut layout, channels.get()) };
        Self(layout)
    }

    pub(crate) fn copied_from(layout: &ff::AVChannelLayout) -> Result<Self> {
        let mut owned = Self(unsafe { std::mem::zeroed() });
        check("av_channel_layout_copy", unsafe {
            ff::av_channel_layout_copy(&mut owned.0, layout)
        })?;
        Ok(owned)
    }

    pub(crate) fn matches(&self, other: &ff::AVChannelLayout) -> bool {
        unsafe { ff::av_channel_layout_compare(&self.0, other) == 0 }
    }
}

impl AsRef<ff::AVChannelLayout> for OwnedChannelLayout {
    fn as_ref(&self) -> &ff::AVChannelLayout {
        &self.0
    }
}

impl Drop for OwnedChannelLayout {
    fn drop(&mut self) {
        unsafe { ff::av_channel_layout_uninit(&mut self.0) };
    }
}

pub(crate) struct OwnedSwr(*mut ff::SwrContext);

impl OwnedSwr {
    pub(crate) fn new(
        out_layout: &ff::AVChannelLayout,
        out_format: ff::AVSampleFormat,
        out_rate: NonZeroI32,
        in_layout: &ff::AVChannelLayout,
        in_format: ff::AVSampleFormat,
        in_rate: c_int,
    ) -> Result<Self> {
        let mut swr = Self(std::ptr::null_mut());
        check("swr_alloc_set_opts2", unsafe {
            ff::swr_alloc_set_opts2(
                &mut swr.0,
                out_layout,
                out_format,
                out_rate.get(),
                in_layout,
                in_format,
                in_rate,
                0,
                std::ptr::null_mut(),
            )
        })?;
        check("swr_init", unsafe { ff::swr_init(swr.0) })?;
        Ok(swr)
    }

    pub(crate) fn apply_delay_and_modify(&self, base: i64) -> i64 {
        unsafe { ff::swr_get_delay(self.0, base) }
    }

    pub(crate) unsafe fn convert(
        &mut self,
        out: *const *mut u8,
        out_count: c_int,
        input: *const *const u8,
        in_count: c_int,
    ) -> Result<c_int> {
        check("swr_convert", unsafe {
            ff::swr_convert(self.0, out, out_count, input, in_count)
        })
    }
}

impl Drop for OwnedSwr {
    fn drop(&mut self) {
        unsafe { ff::swr_free(&mut self.0) };
    }
}

pub(crate) struct OwnedPacket(*mut ff::AVPacket);

impl OwnedPacket {
    pub(crate) fn new() -> Result<Self> {
        let ptr = unsafe { ff::av_packet_alloc() };
        if ptr.is_null() {
            return Err(anyhow!("av_packet_alloc failed"));
        }
        Ok(Self(ptr))
    }

    #[allow(clippy::needless_pass_by_ref_mut)]
    pub(crate) const fn as_mut_ptr(&mut self) -> *mut ff::AVPacket {
        self.0
    }

    pub(crate) fn stream_index(&self) -> c_int {
        unsafe { (*self.0).stream_index }
    }

    pub(crate) fn pts(&self) -> i64 {
        unsafe { (*self.0).pts }
    }

    pub(crate) fn unref(&mut self) {
        unsafe { ff::av_packet_unref(self.0) };
    }
}

impl Drop for OwnedPacket {
    fn drop(&mut self) {
        unsafe { ff::av_packet_free(&mut self.0) };
    }
}
