//! VideoToolbox owns its decode pipeline end to end (no shared immediate context to race on),
//! the plugin blits on its own `MTLCommandQueue`, and `MTLDevice` itself is free-threaded.

use anyhow::{Context as _, Result, anyhow, ensure};
use ffmpeg_sys_next as ff;
use objc2::rc::Retained;
use objc2::runtime::ProtocolObject;
use objc2_metal::{MTLCommandQueue, MTLDevice, MTLResource as _, MTLTexture};
use std::os::raw::c_void;
use std::ptr;

use crate::ffutil::av_err;

/// The engine-provided `MTLDevice`, captured once at init, plus the command
/// queue the plugin blits on. Every clone holds its own ObjC retain, so the
/// device outlives whatever the engine does with its pointer afterwards.
#[derive(Clone)]
pub(crate) struct HwDevice {
    device: Retained<ProtocolObject<dyn MTLDevice>>,
    queue: Retained<ProtocolObject<dyn MTLCommandQueue>>,
}

// Metal devices and command queues are free-threaded
unsafe impl Send for HwDevice {}
unsafe impl Sync for HwDevice {}

impl HwDevice {
    pub(crate) fn metal_device(&self) -> &ProtocolObject<dyn MTLDevice> {
        &self.device
    }

    pub(crate) fn command_queue(&self) -> &ProtocolObject<dyn MTLCommandQueue> {
        &self.queue
    }

    /// Derives the engine device from a live `id<MTLTexture>`
    ///
    /// # Safety
    /// `texture` must be null (rejected) or a live `id<MTLTexture>`. Unlike
    /// COM there is no QueryInterface analog to probe with: a pointer to
    /// anything else is undefined behavior.
    pub(crate) unsafe fn from_texture(texture: *const c_void) -> Result<Self> {
        ensure!(!texture.is_null(), "texture is null");

        let texture: &ProtocolObject<dyn MTLTexture> = unsafe { &*texture.cast() };
        let device = texture.device();
        let queue = device
            .newCommandQueue()
            .context("MTLDevice returned no command queue")?;
        Ok(Self { device, queue })
    }
}

/// A VideoToolbox `AVHWDeviceContext`. Unlike D3D11VA there is no engine
/// device to mirror into it: VideoToolbox needs no device handle, so
/// `av_hwdevice_ctx_create` builds the whole context itself.
pub(crate) struct HwDeviceContext {
    buf: *mut ff::AVBufferRef,
}

// AVBufferRef refcounting is atomic; VideoToolbox serializes its own
// decode sessions internally.
unsafe impl Send for HwDeviceContext {}
unsafe impl Sync for HwDeviceContext {}

impl HwDeviceContext {
    /// Takes no engine device on purpose: VideoToolbox decodes into its own
    /// CVPixelBuffers, and the device only matters later, when the output
    /// wraps them as textures.
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

    /// The decode path is dead on machines where this fails; catching it
    /// here beats debugging a black video texture in the editor.
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
