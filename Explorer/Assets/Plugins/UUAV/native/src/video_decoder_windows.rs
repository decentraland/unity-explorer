use anyhow::{Context, Result, anyhow};
use ffmpeg_sys_next as ff;
use std::ffi::CStr;
use std::os::raw::{c_int, c_void};
use std::ptr;

use crate::ffutil::{
    AVERROR_EAGAIN, Decoded, OwnedDecoder, OwnedFrame, Stream, av_err, check, q2d,
};
use crate::hw_device::HwDeviceContext;

/// A decoded D3D11VA frame. The underlying decoder surface stays alive
/// (and thus valid for GPU copies) for as long as `frame` is held.
pub(crate) struct VideoFrame {
    frame: OwnedFrame,
    pts: Option<f64>,
    width: i32,
    height: i32,
}

impl VideoFrame {
    /// `ID3D11Texture2D*` of the decoder surface pool.
    pub(crate) fn texture_raw(&self) -> *mut c_void {
        self.frame.data(0).cast::<c_void>()
    }

    /// Array slice index inside the decoder's texture array.
    pub(crate) fn subresource(&self) -> u32 {
        let index = self.frame.data(1) as usize;
        u32::try_from(index).unwrap_or(0)
    }

    /// Presentation time in seconds of the media timeline, `None` when the
    /// container provides no usable timestamp.
    pub(crate) const fn pts(&self) -> Option<f64> {
        self.pts
    }

    /// Shifts the presentation time so the media timeline starts at zero.
    pub(crate) fn shift_pts(&mut self, offset: f64) {
        self.pts = self.pts.map(|pts| pts - offset);
    }

    pub(crate) const fn width(&self) -> i32 {
        self.width
    }

    pub(crate) const fn height(&self) -> i32 {
        self.height
    }
}

/// D3D11VA hardware video decoder. Hardware decoding is mandatory: the
/// stream is rejected if the codec offers no D3D11 path or the surfaces
/// are not NV12.
pub(crate) struct VideoDecoder {
    ctx: OwnedDecoder,
    time_base: ff::AVRational,
}

impl VideoDecoder {
    /// Extra surfaces the decoder allocates on top of its own needs; must
    /// cover every frame the player can hold queued at once.
    pub(crate) const EXTRA_HW_FRAMES: c_int = 8;

    pub(crate) fn new(stream: Stream, hw: &HwDeviceContext) -> Result<Self> {
        let codec = stream.find_decoder().context("video stream")?;
        ensure_d3d11_support(codec)?;
        let mut ctx = OwnedDecoder::new(codec)?;

        unsafe {
            let raw = ctx.as_mut_ptr();
            check(
                "avcodec_parameters_to_context(video)",
                ff::avcodec_parameters_to_context(raw, stream.codecpar()),
            )?;
            (*raw).pkt_timebase = stream.time_base();
            (*raw).hw_device_ctx = ff::av_buffer_ref(hw.as_buffer_ptr());
            if (*raw).hw_device_ctx.is_null() {
                return Err(anyhow!("failed to reference the D3D11 device context"));
            }
            (*raw).get_format = Some(require_d3d11_format);
            (*raw).extra_hw_frames = Self::EXTRA_HW_FRAMES;

            check(
                "avcodec_open2(video)",
                ff::avcodec_open2(raw, codec, ptr::null_mut()),
            )?;
        }

        Ok(Self {
            ctx,
            time_base: stream.time_base(),
        })
    }

    /// Sends a packet; pass `null` to signal end of stream.
    pub(crate) fn send(&mut self, packet: *const ff::AVPacket) -> Result<()> {
        let ret = unsafe { ff::avcodec_send_packet(self.ctx.as_mut_ptr(), packet) };
        // EOF from send just means the drain was already signalled
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

        if frame.format() != ff::AVPixelFormat::AV_PIX_FMT_D3D11 as c_int {
            return Err(anyhow!(
                "decoder produced a non-D3D11 frame (format {}); hardware decoding is mandatory",
                frame.format()
            ));
        }

        let sw_format = frame.hw_frames_ctx().context("D3D11 frame")?.sw_format;
        if sw_format != ff::AVPixelFormat::AV_PIX_FMT_NV12 {
            return Err(anyhow!(
                "unsupported D3D11 surface format {:?}; only NV12 is supported",
                sw_format
            ));
        }

        let ts = frame.best_effort_timestamp();
        let pts = if ts == ff::AV_NOPTS_VALUE {
            None
        } else {
            Some(ts as f64 * q2d(self.time_base))
        };
        let width = frame.width();
        let height = frame.height();

        Ok(Decoded::Frame(VideoFrame {
            frame,
            pts,
            width,
            height,
        }))
    }

    pub(crate) fn flush(&mut self) {
        unsafe { ff::avcodec_flush_buffers(self.ctx.as_mut_ptr()) };
    }
}

/// Rejects codecs without a D3D11VA hardware path
/// Software-only decoders never invoke `get_format`, so without
/// this probe the rejection would only happen after a software frame was
/// already decoded.
/// TODO support software fallback next
fn ensure_d3d11_support(codec: *const ff::AVCodec) -> Result<()> {
    for index in 0.. {
        let config = unsafe { ff::avcodec_get_hw_config(codec, index) };
        if config.is_null() {
            break;
        }
        let config = unsafe { &*config };
        let by_device_ctx =
            config.methods & ff::AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX as c_int != 0;
        if by_device_ctx
            && config.device_type == ff::AVHWDeviceType::AV_HWDEVICE_TYPE_D3D11VA
            && config.pix_fmt == ff::AVPixelFormat::AV_PIX_FMT_D3D11
        {
            return Ok(());
        }
    }

    let name = unsafe { CStr::from_ptr(ff::avcodec_get_name((*codec).id)) };
    Err(anyhow!(
        "codec {} has no D3D11 hardware decoder; hardware decoding is mandatory",
        name.to_string_lossy()
    ))
}

/// Rejects everything but the D3D11 hardware pixel format: returning NONE
/// makes `avcodec` fail decoding instead of silently falling back to
/// software frames.
unsafe extern "C" fn require_d3d11_format(
    _ctx: *mut ff::AVCodecContext,
    list: *const ff::AVPixelFormat,
) -> ff::AVPixelFormat {
    unsafe {
        let mut p = list;
        while !p.is_null() && *p != ff::AVPixelFormat::AV_PIX_FMT_NONE {
            if *p == ff::AVPixelFormat::AV_PIX_FMT_D3D11 {
                return ff::AVPixelFormat::AV_PIX_FMT_D3D11;
            }
            p = p.add(1);
        }
    }
    ff::AVPixelFormat::AV_PIX_FMT_NONE
}
