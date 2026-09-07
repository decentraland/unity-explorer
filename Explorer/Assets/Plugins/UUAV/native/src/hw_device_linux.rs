//! VAAPI decodes on its own `VADisplay` over the device's DRM render
//! node; the Vulkan side ([`crate::linux_device::HeadlessDevice`])
//! receives decoded surfaces as dma-buf imports, so the two APIs share
//! the GPU through memory, not through a context.

use anyhow::{Context as _, Result, anyhow, ensure};
use ffmpeg_sys_next as ff;
use std::ffi::CString;
use std::os::raw::c_void;
use std::ptr;
use std::sync::Arc;

use crate::ffutil::av_err;
use crate::linux_device::HeadlessDevice;

/// The helper-created Vulkan device, adopted once at init. Every clone
/// holds its own `Arc` reference, so the device outlives whatever the
/// caller does with its pointer afterwards.
#[derive(Clone)]
pub(crate) struct HwDevice {
    inner: Arc<HeadlessDevice>,
}

impl HwDevice {
    pub(crate) const fn headless(&self) -> &Arc<HeadlessDevice> {
        &self.inner
    }

    /// Adopts the magic-tagged [`HeadlessDevice`] probe pointer. Unlike
    /// the other platforms there is no engine texture on Linux — the
    /// helper owns the device and the probe pointer names it.
    ///
    /// # Safety
    /// `texture` must be null (rejected) or a pointer obtained from
    /// [`HeadlessDevice::probe_ptr`] on a still-live device.
    pub(crate) unsafe fn from_texture(texture: *const c_void) -> Result<Self> {
        let inner = unsafe { HeadlessDevice::adopt_probe_ptr(texture) }?;
        Ok(Self { inner })
    }
}

/// A VAAPI `AVHWDeviceContext` opened on the same GPU's render node, so
/// decoded surfaces export as dma-bufs the Vulkan device can import.
pub(crate) struct HwDeviceContext {
    buf: *mut ff::AVBufferRef,
}

// AVBufferRef refcounting is atomic; libva serializes per-display state
// internally.
unsafe impl Send for HwDeviceContext {}
unsafe impl Sync for HwDeviceContext {}

impl HwDeviceContext {
    /// Mirrors the D3D11 arity (the device carries which GPU to open);
    /// the context itself is FFmpeg-owned, like VideoToolbox.
    pub(crate) fn new(device: &HwDevice) -> Result<Self> {
        let node = device
            .headless()
            .render_node()
            .ok_or_else(|| anyhow!("Vulkan device has no DRM render node; VAAPI decode is impossible"))?;
        let node = CString::new(node).context("render node path")?;

        let mut buf: *mut ff::AVBufferRef = ptr::null_mut();
        let ret = unsafe {
            ff::av_hwdevice_ctx_create(
                &mut buf,
                ff::AVHWDeviceType::AV_HWDEVICE_TYPE_VAAPI,
                node.as_ptr(),
                ptr::null_mut(),
                0,
            )
        };
        if ret < 0 {
            return Err(av_err("av_hwdevice_ctx_create(VAAPI)", ret));
        }
        ensure!(!buf.is_null(), "av_hwdevice_ctx_create(VAAPI) returned no context");
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
    /// here beats debugging a black video texture in the editor. Skips
    /// (rather than fails) where no render node exists — CI runners have
    /// no GPU.
    #[test]
    fn vaapi_hwdevice_is_creatable() {
        let node = c"/dev/dri/renderD128";
        if !std::path::Path::new("/dev/dri/renderD128").exists() {
            eprintln!("skipping: no /dev/dri/renderD128 on this machine");
            return;
        }
        let mut buf: *mut ff::AVBufferRef = ptr::null_mut();
        let ret = unsafe {
            ff::av_hwdevice_ctx_create(
                &mut buf,
                ff::AVHWDeviceType::AV_HWDEVICE_TYPE_VAAPI,
                node.as_ptr(),
                ptr::null_mut(),
                0,
            )
        };
        assert!(ret >= 0, "av_hwdevice_ctx_create(VAAPI): {ret}");
        assert!(!buf.is_null());
        unsafe { ff::av_buffer_unref(&mut buf) };
    }
}
