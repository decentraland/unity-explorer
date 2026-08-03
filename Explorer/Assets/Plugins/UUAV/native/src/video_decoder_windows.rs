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

#[path = "sw_nv12.rs"]
mod sw_nv12;
pub(crate) use sw_nv12::Nv12Buffer;
use sw_nv12::Nv12Converter;

pub(crate) struct VideoFrame {
    frame: OwnedFrame,
    converted: Option<Nv12Buffer>,
    pts: Option<f64>,
    info: FrameInfo,
}

impl VideoFrame {
    pub(crate) fn texture_raw(&self) -> *mut c_void {
        self.frame.data(0).cast::<c_void>()
    }

    pub(crate) fn subresource(&self) -> u32 {
        let index = self.frame.data(1) as usize;
        u32::try_from(index).unwrap_or(0)
    }

    pub(crate) const fn software_nv12(&self) -> Option<&Nv12Buffer> {
        self.converted.as_ref()
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

pub(crate) struct VideoDecoder {
    ctx: OwnedDecoder,
    time_base: ff::AVRational,
    converter: Option<Nv12Converter>,
    logged_path: bool,
}

impl VideoDecoder {
    pub(crate) const EXTRA_HW_FRAMES: c_int = 8;

    pub(crate) fn new(stream: Stream, hw: &HwDeviceContext) -> Result<Self> {
        let codec = stream.find_decoder().context("video stream")?;
        let hardware_capable = has_d3d11_config(codec);
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
                    return Err(anyhow!("failed to reference the D3D11 device context"));
                }
                (*raw).get_format = Some(prefer_d3d11_format);
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
            logged_path: false,
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

        if !self.logged_path {
            self.logged_path = true;
            let hw = frame.format() == ff::AVPixelFormat::AV_PIX_FMT_D3D11 as c_int;
            crate::diag_log(if hw {
                "uuav-core: decode=hw"
            } else {
                "uuav-core: decode=sw"
            });
        }

        let (converted, bit_depth) =
            if frame.format() == ff::AVPixelFormat::AV_PIX_FMT_D3D11 as c_int {
                use ff::AVPixelFormat::{AV_PIX_FMT_NV12, AV_PIX_FMT_P010LE};
                let sw_format = frame.hw_frames_ctx().context("D3D11 frame")?.sw_format;
                let depth = match sw_format {
                    AV_PIX_FMT_NV12 => 8,
                    AV_PIX_FMT_P010LE => 10,
                    other => {
                        return Err(anyhow!(
                            "unsupported D3D11 surface format {other:?}; \
                             expected NV12 (8-bit) or P010 (10-bit)"
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

fn has_d3d11_config(codec: *const ff::AVCodec) -> bool {
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
            return true;
        }
    }
    false
}

unsafe extern "C" fn prefer_d3d11_format(
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
        if list.is_null() {
            ff::AVPixelFormat::AV_PIX_FMT_NONE
        } else {
            *list
        }
    }
}
