
use anyhow::{Result, anyhow, ensure};
use ffmpeg_sys_next as ff;
use objc2::rc::Retained;
use objc2::runtime::ProtocolObject;
use objc2_metal::{MTLDevice, MTLResource as _, MTLTexture};
use std::os::raw::c_void;
use std::ptr;

use crate::ffutil::av_err;

#[derive(Clone)]
pub(crate) struct HwDevice {
    device: Retained<ProtocolObject<dyn MTLDevice>>,
}

unsafe impl Send for HwDevice {}
unsafe impl Sync for HwDevice {}

impl HwDevice {
    pub(crate) fn metal_device(&self) -> &ProtocolObject<dyn MTLDevice> {
        &self.device
    }

    pub(crate) unsafe fn from_texture(texture: *const c_void) -> Result<Self> {
        ensure!(!texture.is_null(), "texture is null");

        let texture: &ProtocolObject<dyn MTLTexture> = unsafe { &*texture.cast() };
        Ok(Self {
            device: texture.device(),
        })
    }
}

pub(crate) struct HwDeviceContext {
    buf: *mut ff::AVBufferRef,
}

unsafe impl Send for HwDeviceContext {}
unsafe impl Sync for HwDeviceContext {}

impl HwDeviceContext {
    pub(crate) fn new() -> Result<Self> {
        let mut buf: *mut ff::AVBufferRef = ptr::null_mut();
        let ret = unsafe {
            ff::av_hwdevice_ctx_create(
                &mut buf,
                ff::AVHWDeviceType::AV_HWDEVICE_TYPE_VIDEOTOOLBOX,
                ptr::null(),
                ptr::null_mut(),
                0,
            )
        };
        if ret < 0 {
            return Err(av_err("av_hwdevice_ctx_create(VIDEOTOOLBOX)", ret));
        }
        if buf.is_null() {
            return Err(anyhow!(
                "av_hwdevice_ctx_create(VIDEOTOOLBOX) returned no context"
            ));
        }
        Ok(Self { buf })
    }

    pub(crate) const fn as_buffer_ptr(&self) -> *mut ff::AVBufferRef {
        self.buf
    }
}

impl Drop for HwDeviceContext {
    fn drop(&mut self) {
        unsafe { ff::av_buffer_unref(&mut self.buf) };
    }
}

#[cfg(test)]
mod tests {
    use ffmpeg_sys_next as ff;
    use std::ptr;

    #[test]
    fn videotoolbox_hwdevice_is_creatable() {
        let mut buf: *mut ff::AVBufferRef = ptr::null_mut();
        let ret = unsafe {
            ff::av_hwdevice_ctx_create(
                &mut buf,
                ff::AVHWDeviceType::AV_HWDEVICE_TYPE_VIDEOTOOLBOX,
                ptr::null(),
                ptr::null_mut(),
                0,
            )
        };
        assert!(ret >= 0, "av_hwdevice_ctx_create(VIDEOTOOLBOX): {ret}");
        assert!(!buf.is_null());
        unsafe { ff::av_buffer_unref(&mut buf) };
    }
}
