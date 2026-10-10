//! Unity-device capture from the probe texture (D3D11).

use anyhow::{Context as _, Result, ensure};
use std::os::raw::c_void;
use windows::Win32::Graphics::Direct3D11::{
    ID3D11Device, ID3D11Device1, ID3D11DeviceContext, ID3D11Texture2D,
};
use windows::Win32::Graphics::Dxgi::IDXGIDevice;
use windows::core::{IUnknown, Interface as _};

/// Unity's D3D11 device, retained past the probe texture's lifetime, plus
/// the interfaces presentation copies run on.
pub struct UnityDevice {
    /// The `ID3D11Device1` face of Unity's device; `OpenSharedResource1`
    /// (NT-handle sharing) lives here, plane textures are created on it.
    pub device: ID3D11Device1,
    /// Unity's immediate context. Only touched inside the render-event
    /// callback — Unity's own submission thread.
    pub context: ID3D11DeviceContext,
    /// Packed LUID of the adapter, sent to the helper so it decodes on the
    /// same GPU (shared textures cannot cross adapters).
    pub adapter_luid: u64,
}

// D3D11 devices are free-threaded; the context stays on the render thread
// (see field docs)
unsafe impl Send for UnityDevice {}
unsafe impl Sync for UnityDevice {}

/// Captures the device behind Unity's probe texture (the probe itself is
/// destroyed by C# right after init).
///
/// # Safety
/// `texture` must be null (rejected) or a live COM pointer; anything that is
/// not an `ID3D11Texture2D` fails the QueryInterface probe cleanly.
pub unsafe fn capture_probe(texture: *const c_void) -> Result<UnityDevice> {
    ensure!(!texture.is_null(), "texture is null");

    let raw = texture.cast_mut();
    let unknown = unsafe { IUnknown::from_raw_borrowed(&raw) }.context("texture is null")?;
    let texture: ID3D11Texture2D = unknown
        .cast()
        .context("probe texture is not an ID3D11Texture2D")?;

    let device: ID3D11Device =
        unsafe { texture.GetDevice() }.context("probe texture has no device")?;
    let device1: ID3D11Device1 = device
        .cast()
        .context("device is not an ID3D11Device1 (NT-handle sharing needs WDDM 1.2+)")?;
    let context = unsafe { device.GetImmediateContext() }
        .context("device has no immediate context")?;

    let dxgi: IDXGIDevice = device.cast().context("device is not an IDXGIDevice")?;
    let adapter = unsafe { dxgi.GetAdapter() }.context("GetAdapter failed")?;
    let desc = unsafe { adapter.GetDesc() }.context("GetDesc failed")?;
    let adapter_luid =
        (u64::from(desc.AdapterLuid.HighPart as u32) << 32) | u64::from(desc.AdapterLuid.LowPart);

    Ok(UnityDevice {
        device: device1,
        context,
        adapter_luid,
    })
}
