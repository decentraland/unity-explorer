
use anyhow::{Context as _, Result, anyhow};
use ffmpeg_sys_next as ff;
use std::os::raw::c_int;
use std::ptr;

use crate::ffutil::OwnedFrame;

pub(crate) struct Nv12Buffer {
    data: Vec<u8>,
    width: c_int,
    height: c_int,
}

impl Nv12Buffer {
    pub(crate) const fn width(&self) -> u32 {
        self.width as u32
    }

    pub(crate) const fn height(&self) -> u32 {
        self.height as u32
    }

    pub(crate) const fn stride(&self) -> u32 {
        self.width as u32
    }

    pub(crate) fn bytes(&self) -> &[u8] {
        &self.data
    }
}

pub(crate) struct Nv12Converter {
    sws: *mut ff::SwsContext,
    width: c_int,
    height: c_int,
    src_format: c_int,
}

unsafe impl Send for Nv12Converter {}

impl Nv12Converter {
    pub(crate) const fn new() -> Self {
        Self {
            sws: ptr::null_mut(),
            width: 0,
            height: 0,
            src_format: ff::AVPixelFormat::AV_PIX_FMT_NONE as c_int,
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
                width & !1,
                height & !1,
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

        self.width = width;
        self.height = height;
        self.src_format = src_format;
        Ok(())
    }

    pub(crate) fn convert(&mut self, frame: &OwnedFrame) -> Result<Nv12Buffer> {
        let src_width = frame.width();
        let src_height = frame.height();
        let width = src_width & !1;
        let height = src_height & !1;
        if width <= 0 || height <= 0 {
            return Err(anyhow!("software frame has no presentable size"));
        }
        self.ensure_context(src_width, src_height, frame.format())?;

        let stride = width as usize;
        let luma = stride
            .checked_mul(height as usize)
            .context("NV12 luma plane size overflows")?;
        let total = luma
            .checked_add(luma / 2)
            .context("NV12 image size overflows")?;
        let mut data = vec![0u8; total];
        let (y_plane, uv_plane) = data
            .split_at_mut_checked(luma)
            .context("NV12 buffer shorter than its luma plane")?;

        let dst_data: [*mut u8; 4] = [
            y_plane.as_mut_ptr(),
            uv_plane.as_mut_ptr(),
            ptr::null_mut(),
            ptr::null_mut(),
        ];
        let dst_stride: [c_int; 4] = [width, width, 0, 0];
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
                src_height,
                dst_data.as_ptr(),
                dst_stride.as_ptr(),
            )
        };
        if rows != height {
            return Err(anyhow!("sws_scale converted {rows} of {height} rows"));
        }
        Ok(Nv12Buffer {
            data,
            width,
            height,
        })
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
        }
    }
}
