
use anyhow::{Context, Result, anyhow};
use ffmpeg_sys_next as ff;
use std::os::raw::{c_int, c_void};
use std::ptr;

use crate::ffutil::{
    AVERROR_EAGAIN, Decoded, OwnedDecoder, OwnedFrame, Stream, apply_decode_limits, av_err, check,
    q2d,
};
use crate::frame_info::FrameInfo;
use crate::hw_device::HwDeviceContext;

type CFTypeRef = *const c_void;
type CFAllocatorRef = *const c_void;
type CFDictionaryRef = *const c_void;
type CVPixelBufferRef = *mut c_void;

#[allow(non_upper_case_globals)]
unsafe extern "C" {
    static kCFAllocatorDefault: CFAllocatorRef;
    static kCFTypeDictionaryKeyCallBacks: c_void;
    static kCFTypeDictionaryValueCallBacks: c_void;
    static kCVPixelBufferIOSurfacePropertiesKey: CFTypeRef;
    static kCVPixelBufferWidthKey: CFTypeRef;
    static kCVPixelBufferHeightKey: CFTypeRef;
    static kCVPixelBufferPixelFormatTypeKey: CFTypeRef;

    fn CFDictionaryCreateMutable(
        allocator: CFAllocatorRef,
        capacity: isize,
        key_callbacks: *const c_void,
        value_callbacks: *const c_void,
    ) -> *mut c_void;
    fn CFDictionarySetValue(dict: *mut c_void, key: *const c_void, value: *const c_void);
    fn CFRelease(cf: CFTypeRef);

    fn CFNumberCreate(
        allocator: CFAllocatorRef,
        number_type: i32,
        value: *const c_void,
    ) -> CFTypeRef;
    fn CVPixelBufferPoolCreate(
        allocator: CFAllocatorRef,
        pool_attributes: CFDictionaryRef,
        pixel_buffer_attributes: CFDictionaryRef,
        pool_out: *mut *mut c_void,
    ) -> i32;
    fn CVPixelBufferPoolCreatePixelBuffer(
        allocator: CFAllocatorRef,
        pool: *mut c_void,
        pixel_buffer_out: *mut CVPixelBufferRef,
    ) -> i32;
    fn CVPixelBufferLockBaseAddress(pb: CVPixelBufferRef, flags: u64) -> i32;
    fn CVPixelBufferUnlockBaseAddress(pb: CVPixelBufferRef, flags: u64) -> i32;
    fn CVPixelBufferGetBaseAddressOfPlane(pb: CVPixelBufferRef, plane: usize) -> *mut c_void;
    fn CVPixelBufferGetBytesPerRowOfPlane(pb: CVPixelBufferRef, plane: usize) -> usize;
}

const PIXEL_FORMAT_NV12_VIDEO_RANGE: u32 = 0x3432_3076;

struct OwnedPixelBuffer(CVPixelBufferRef);

unsafe impl Send for OwnedPixelBuffer {}

impl Drop for OwnedPixelBuffer {
    fn drop(&mut self) {
        if !self.0.is_null() {
            unsafe { CFRelease(self.0.cast_const()) };
        }
    }
}

pub(crate) struct VideoFrame {
    frame: OwnedFrame,
    converted: Option<OwnedPixelBuffer>,
    pts: Option<f64>,
    info: FrameInfo,
}

impl VideoFrame {
    pub(crate) fn pixel_buffer(&self) -> *mut c_void {
        match &self.converted {
            Some(buffer) => buffer.0,
            None => self.frame.data(3).cast::<c_void>(),
        }
    }

    pub(crate) const fn info(&self) -> FrameInfo {
        self.info
    }

    pub(crate) const fn pts(&self) -> Option<f64> {
        self.pts
    }

    pub(crate) fn shift_pts(&mut self, offset: f64) {
        self.pts = self.pts.map(|pts| pts - offset);
    }
}

struct Nv12Converter {
    sws: *mut ff::SwsContext,
    pool: *mut c_void,
    width: c_int,
    height: c_int,
    src_format: c_int,
}

unsafe impl Send for Nv12Converter {}

const CF_NUMBER_SINT32: i32 = 3;

impl Nv12Converter {
    const fn new() -> Self {
        Self {
            sws: ptr::null_mut(),
            pool: ptr::null_mut(),
            width: 0,
            height: 0,
            src_format: ff::AVPixelFormat::AV_PIX_FMT_NONE as c_int,
        }
    }

    fn make_pool(width: c_int, height: c_int) -> Result<*mut c_void> {
        unsafe {
            let attributes = CFDictionaryCreateMutable(
                kCFAllocatorDefault,
                4,
                ptr::addr_of!(kCFTypeDictionaryKeyCallBacks),
                ptr::addr_of!(kCFTypeDictionaryValueCallBacks),
            );
            if attributes.is_null() {
                return Err(anyhow!("failed to allocate pixel buffer attributes"));
            }
            let mut owned = vec![];

            let empty = CFDictionaryCreateMutable(
                kCFAllocatorDefault,
                0,
                ptr::addr_of!(kCFTypeDictionaryKeyCallBacks),
                ptr::addr_of!(kCFTypeDictionaryValueCallBacks),
            );
            if empty.is_null() {
                CFRelease(attributes.cast_const());
                return Err(anyhow!("failed to allocate IOSurface properties"));
            }
            CFDictionarySetValue(
                attributes,
                kCVPixelBufferIOSurfacePropertiesKey.cast(),
                empty.cast_const(),
            );
            owned.push(empty.cast_const());

            let mut set_number = |key: CFTypeRef, value: i32| -> bool {
                let number = CFNumberCreate(
                    kCFAllocatorDefault,
                    CF_NUMBER_SINT32,
                    ptr::addr_of!(value).cast(),
                );
                if number.is_null() {
                    return false;
                }
                CFDictionarySetValue(attributes, key.cast(), number.cast());
                owned.push(number);
                true
            };

            let ok = set_number(kCVPixelBufferWidthKey, width)
                && set_number(kCVPixelBufferHeightKey, height)
                && set_number(
                    kCVPixelBufferPixelFormatTypeKey,
                    PIXEL_FORMAT_NV12_VIDEO_RANGE.cast_signed(),
                );

            let mut pool: *mut c_void = ptr::null_mut();
            let status = if ok {
                CVPixelBufferPoolCreate(
                    kCFAllocatorDefault,
                    ptr::null(),
                    attributes.cast_const(),
                    &mut pool,
                )
            } else {
                -1
            };

            for object in owned {
                CFRelease(object);
            }
            CFRelease(attributes.cast_const());

            if status != 0 || pool.is_null() {
                return Err(anyhow!("CVPixelBufferPoolCreate failed (status {status})"));
            }
            Ok(pool)
        }
    }

    fn ensure_context(&mut self, width: c_int, height: c_int, src_format: c_int) -> Result<()> {
        if !self.sws.is_null()
            && self.width == width
            && self.height == height
            && self.src_format == src_format
        {
            return Ok(());
        }

        let src = unsafe { std::mem::transmute::<c_int, ff::AVPixelFormat>(src_format) };
        let sws = unsafe {
            ff::sws_getCachedContext(
                self.sws,
                width,
                height,
                without_range_flag(src),
                width,
                height,
                ff::AVPixelFormat::AV_PIX_FMT_NV12,
                ff::SwsFlags::SWS_BILINEAR as c_int,
                ptr::null_mut(),
                ptr::null_mut(),
                ptr::null(),
            )
        };
        self.sws = sws;
        if sws.is_null() {
            return Err(anyhow!(
                "no swscale conversion from pixel format {src_format} to NV12"
            ));
        }

        let pool = Self::make_pool(width, height)?;
        unsafe {
            if !self.pool.is_null() {
                CFRelease(self.pool.cast_const());
            }
        }
        self.pool = pool;
        self.width = width;
        self.height = height;
        self.src_format = src_format;
        Ok(())
    }

    fn convert(&mut self, frame: &OwnedFrame) -> Result<OwnedPixelBuffer> {
        let width = frame.width();
        let height = frame.height();
        if width <= 0 || height <= 0 {
            return Err(anyhow!("software frame has no presentable size"));
        }
        self.ensure_context(width, height, frame.format())?;

        let mut raw: CVPixelBufferRef = ptr::null_mut();
        let status =
            unsafe { CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, self.pool, &mut raw) };
        if status != 0 || raw.is_null() {
            return Err(anyhow!(
                "CVPixelBufferPoolCreatePixelBuffer failed (status {status})"
            ));
        }
        let buffer = OwnedPixelBuffer(raw);

        let locked = unsafe { CVPixelBufferLockBaseAddress(raw, 0) };
        if locked != 0 {
            return Err(anyhow!("CVPixelBufferLockBaseAddress failed ({locked})"));
        }

        let scaled = (|| -> Result<()> {
            let y = unsafe { CVPixelBufferGetBaseAddressOfPlane(raw, 0) };
            let uv = unsafe { CVPixelBufferGetBaseAddressOfPlane(raw, 1) };
            if y.is_null() || uv.is_null() {
                return Err(anyhow!("CVPixelBuffer has no plane base addresses"));
            }
            let dst_data: [*mut u8; 4] = [
                y.cast::<u8>(),
                uv.cast::<u8>(),
                ptr::null_mut(),
                ptr::null_mut(),
            ];
            let dst_stride: [c_int; 4] = [
                c_int::try_from(unsafe { CVPixelBufferGetBytesPerRowOfPlane(raw, 0) })
                    .context("plane 0 stride")?,
                c_int::try_from(unsafe { CVPixelBufferGetBytesPerRowOfPlane(raw, 1) })
                    .context("plane 1 stride")?,
                0,
                0,
            ];
            let src_data: [*const u8; 4] = [
                frame.data(0).cast_const(),
                frame.data(1).cast_const(),
                frame.data(2).cast_const(),
                frame.data(3).cast_const(),
            ];
            let src_stride: [c_int; 4] = [
                frame.linesize(0),
                frame.linesize(1),
                frame.linesize(2),
                frame.linesize(3),
            ];

            let rows = unsafe {
                ff::sws_scale(
                    self.sws,
                    src_data.as_ptr(),
                    src_stride.as_ptr(),
                    0,
                    height,
                    dst_data.as_ptr(),
                    dst_stride.as_ptr(),
                )
            };
            if rows != height {
                return Err(anyhow!("sws_scale converted {rows} of {height} rows"));
            }
            Ok(())
        })();

        unsafe { CVPixelBufferUnlockBaseAddress(raw, 0) };
        scaled?;
        Ok(buffer)
    }
}

const fn without_range_flag(format: ff::AVPixelFormat) -> ff::AVPixelFormat {
    use ff::AVPixelFormat::{
        AV_PIX_FMT_YUV411P, AV_PIX_FMT_YUV420P, AV_PIX_FMT_YUV422P, AV_PIX_FMT_YUV440P,
        AV_PIX_FMT_YUV444P, AV_PIX_FMT_YUVJ411P, AV_PIX_FMT_YUVJ420P, AV_PIX_FMT_YUVJ422P,
        AV_PIX_FMT_YUVJ440P, AV_PIX_FMT_YUVJ444P,
    };
    match format {
        AV_PIX_FMT_YUVJ420P => AV_PIX_FMT_YUV420P,
        AV_PIX_FMT_YUVJ422P => AV_PIX_FMT_YUV422P,
        AV_PIX_FMT_YUVJ444P => AV_PIX_FMT_YUV444P,
        AV_PIX_FMT_YUVJ440P => AV_PIX_FMT_YUV440P,
        AV_PIX_FMT_YUVJ411P => AV_PIX_FMT_YUV411P,
        plain => plain,
    }
}

impl Drop for Nv12Converter {
    fn drop(&mut self) {
        unsafe {
            if !self.sws.is_null() {
                ff::sws_freeContext(self.sws);
            }
            if !self.pool.is_null() {
                CFRelease(self.pool.cast_const());
            }
        }
    }
}

pub(crate) struct VideoDecoder {
    ctx: OwnedDecoder,
    time_base: ff::AVRational,
    converter: Option<Nv12Converter>,
}

impl VideoDecoder {
    pub(crate) const EXTRA_HW_FRAMES: c_int = 8;

    pub(crate) fn new(stream: Stream, hw: &HwDeviceContext) -> Result<Self> {
        let codec = stream.find_decoder().context("video stream")?;
        let hardware_capable = has_videotoolbox_config(codec);
        let mut ctx = OwnedDecoder::new(codec)?;

        unsafe {
            let raw = ctx.as_mut_ptr();
            check(
                "avcodec_parameters_to_context(video)",
                ff::avcodec_parameters_to_context(raw, stream.codecpar()),
            )?;
            (*raw).pkt_timebase = stream.time_base();

            if hardware_capable {
                (*raw).hw_device_ctx = ff::av_buffer_ref(hw.as_buffer_ptr());
                if (*raw).hw_device_ctx.is_null() {
                    return Err(anyhow!(
                        "failed to reference the VideoToolbox device context"
                    ));
                }
                (*raw).get_format = Some(prefer_videotoolbox_format);
                (*raw).extra_hw_frames = Self::EXTRA_HW_FRAMES;
            }
            apply_decode_limits(raw);

            check(
                "avcodec_open2(video)",
                ff::avcodec_open2(raw, codec, ptr::null_mut()),
            )?;
        }

        Ok(Self {
            ctx,
            time_base: stream.time_base(),
            converter: None,
        })
    }

    pub(crate) fn send(&mut self, packet: *const ff::AVPacket) -> Result<()> {
        let ret = unsafe { ff::avcodec_send_packet(self.ctx.as_mut_ptr(), packet) };
        if ret < 0 && ret != ff::AVERROR_EOF {
            return Err(av_err("avcodec_send_packet(video)", ret));
        }
        Ok(())
    }

    pub(crate) fn receive(&mut self) -> Result<Decoded<VideoFrame>> {
        let mut frame = OwnedFrame::new()?;
        let ret = unsafe { ff::avcodec_receive_frame(self.ctx.as_mut_ptr(), frame.as_mut_ptr()) };
        if ret == AVERROR_EAGAIN {
            return Ok(Decoded::Again);
        }
        if ret == ff::AVERROR_EOF {
            return Ok(Decoded::Eof);
        }
        check("avcodec_receive_frame(video)", ret)?;

        let (converted, bit_depth) =
            if frame.format() == ff::AVPixelFormat::AV_PIX_FMT_VIDEOTOOLBOX as c_int {
                use ff::AVPixelFormat::{
                    AV_PIX_FMT_NV12, AV_PIX_FMT_NV16, AV_PIX_FMT_NV24, AV_PIX_FMT_P010LE,
                    AV_PIX_FMT_P210LE, AV_PIX_FMT_P410LE,
                };
                let sw_format = frame
                    .hw_frames_ctx()
                    .context("VideoToolbox frame")?
                    .sw_format;
                let depth = match sw_format {
                    AV_PIX_FMT_NV12 | AV_PIX_FMT_NV16 | AV_PIX_FMT_NV24 => 8,
                    AV_PIX_FMT_P010LE | AV_PIX_FMT_P210LE | AV_PIX_FMT_P410LE => 10,
                    other => {
                        return Err(anyhow!(
                            "unsupported VideoToolbox surface format {other:?}; \
                             expected a semi-planar 8- or 10-bit YUV surface"
                        ));
                    }
                };
                (None, depth)
            } else {
                let converter = match self.converter {
                    Some(ref mut converter) => converter,
                    None => self.converter.insert(Nv12Converter::new()),
                };
                (Some(converter.convert(&frame)?), 8)
            };

        let ts = frame.best_effort_timestamp();
        let pts = if ts == ff::AV_NOPTS_VALUE {
            None
        } else {
            Some(ts as f64 * q2d(self.time_base))
        };
        let info = FrameInfo::of(&frame, bit_depth);

        Ok(Decoded::Frame(VideoFrame {
            frame,
            converted,
            pts,
            info,
        }))
    }

    pub(crate) fn flush(&mut self) {
        unsafe { ff::avcodec_flush_buffers(self.ctx.as_mut_ptr()) };
    }
}

fn has_videotoolbox_config(codec: *const ff::AVCodec) -> bool {
    for index in 0.. {
        let config = unsafe { ff::avcodec_get_hw_config(codec, index) };
        if config.is_null() {
            break;
        }
        let config = unsafe { &*config };
        let by_device_ctx =
            config.methods & ff::AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX as c_int != 0;
        if by_device_ctx
            && config.device_type == ff::AVHWDeviceType::AV_HWDEVICE_TYPE_VIDEOTOOLBOX
            && config.pix_fmt == ff::AVPixelFormat::AV_PIX_FMT_VIDEOTOOLBOX
        {
            return true;
        }
    }
    false
}

unsafe extern "C" fn prefer_videotoolbox_format(
    _ctx: *mut ff::AVCodecContext,
    list: *const ff::AVPixelFormat,
) -> ff::AVPixelFormat {
    unsafe {
        let mut p = list;
        while !p.is_null() && *p != ff::AVPixelFormat::AV_PIX_FMT_NONE {
            if *p == ff::AVPixelFormat::AV_PIX_FMT_VIDEOTOOLBOX {
                return ff::AVPixelFormat::AV_PIX_FMT_VIDEOTOOLBOX;
            }
            p = p.add(1);
        }
        if list.is_null() {
            ff::AVPixelFormat::AV_PIX_FMT_NONE
        } else {
            *list
        }
    }
}
