use anyhow::{Context as _, Result, ensure};
use std::os::raw::c_void;
use windows::Win32::Graphics::Direct3D11::{ID3D11Multithread, ID3D11Texture2D};
use windows::core::IUnknown;

use anyhow::anyhow;
use ffmpeg_sys_next as ff;
use windows::Win32::Graphics::Direct3D11::ID3D11Device;
use windows::core::Interface;

use crate::ffutil::av_err;

#[derive(Clone)]
pub(crate) struct HwDevice {
    device: ID3D11Device,
}

unsafe impl Send for HwDevice {}
unsafe impl Sync for HwDevice {}

impl HwDevice {
    pub(crate) const fn device(&self) -> &ID3D11Device {
        &self.device
    }

    pub(crate) unsafe fn from_texture(texture: *const c_void) -> Result<Self> {
        ensure!(!texture.is_null(), "texture is null");

        let raw = texture.cast_mut();
        let unknown =
            unsafe { IUnknown::from_raw_borrowed(&raw) }.context("texture is not a COM pointer")?;

        let texture: ID3D11Texture2D = unknown
            .cast()
            .context("texture is not an ID3D11Texture2D (is the engine running D3D11?)")?;

        let device = unsafe { texture.GetDevice() }.context("texture has no device")?;

        unsafe {
            let immediate = device
                .GetImmediateContext()
                .context("device has no immediate context")?;
            let multithread: ID3D11Multithread = immediate
                .cast()
                .context("immediate context exposes no ID3D11Multithread")?;
            let _ = multithread.SetMultithreadProtected(true);
        }

        Ok(Self { device })
    }
}

#[repr(C)]
struct AVD3D11VADeviceContext {
    device: *mut c_void,
    device_context: *mut c_void,
    video_device: *mut c_void,
    video_context: *mut c_void,
    lock: Option<unsafe extern "C" fn(*mut c_void)>,
    unlock: Option<unsafe extern "C" fn(*mut c_void)>,
    lock_ctx: *mut c_void,
    bind_flags: u32,
    misc_flags: u32,
}

pub(crate) struct HwDeviceContext {
    buf: *mut ff::AVBufferRef,
}

unsafe impl Send for HwDeviceContext {}
unsafe impl Sync for HwDeviceContext {}

impl HwDeviceContext {
    pub(crate) fn new(device: &HwDevice) -> Result<Self> {
        unsafe {
            let mut buf = ff::av_hwdevice_ctx_alloc(ff::AVHWDeviceType::AV_HWDEVICE_TYPE_D3D11VA);
            if buf.is_null() {
                return Err(anyhow!("av_hwdevice_ctx_alloc(D3D11VA) failed"));
            }

            let dev_ctx = (*buf).data.cast::<ff::AVHWDeviceContext>();
            let hwctx = (*dev_ctx).hwctx.cast::<AVD3D11VADeviceContext>();

            (*hwctx).device = device.device().clone().into_raw();

            let ret = ff::av_hwdevice_ctx_init(buf);
            if ret < 0 {
                ff::av_buffer_unref(&mut buf);
                return Err(av_err("av_hwdevice_ctx_init(D3D11VA)", ret));
            }

            Ok(Self { buf })
        }
    }

    pub(crate) const fn as_buffer_ptr(&self) -> *mut ff::AVBufferRef {
        self.buf
    }

    fn hwctx(&self) -> *mut AVD3D11VADeviceContext {
        unsafe {
            let dev_ctx = (*self.buf).data.cast::<ff::AVHWDeviceContext>();
            (*dev_ctx).hwctx.cast::<AVD3D11VADeviceContext>()
        }
    }

    pub(crate) fn immediate_context_raw(&self) -> *mut c_void {
        unsafe { (*self.hwctx()).device_context }
    }

    pub(crate) fn lock(&self) -> HwLockGuard<'_> {
        unsafe {
            let hwctx = self.hwctx();
            if let Some(lock) = (*hwctx).lock {
                lock((*hwctx).lock_ctx);
            }
        }
        HwLockGuard { ctx: self }
    }
}

impl Drop for HwDeviceContext {
    fn drop(&mut self) {
        unsafe { ff::av_buffer_unref(&mut self.buf) };
    }
}

pub(crate) struct HwLockGuard<'a> {
    ctx: &'a HwDeviceContext,
}

impl Drop for HwLockGuard<'_> {
    fn drop(&mut self) {
        unsafe {
            let hwctx = self.ctx.hwctx();
            if let Some(unlock) = (*hwctx).unlock {
                unlock((*hwctx).lock_ctx);
            }
        }
    }
}
