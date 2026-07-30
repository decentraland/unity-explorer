//! Unity-device introspection from the probe texture (D3D11).

use anyhow::{Context as _, Result, ensure};
use std::os::raw::c_void;
use windows::Win32::Graphics::Direct3D11::{ID3D11Device, ID3D11Texture2D};
use windows::Win32::Graphics::Dxgi::IDXGIDevice;
use windows::core::{IUnknown, Interface as _};

/// The packed LUID of the adapter behind Unity's probe texture; the helper
/// must create its decode device on the same adapter or the shared textures
/// cannot be opened cross-process.
///
/// # Safety
/// `texture` must be null (rejected) or a live COM pointer; anything that is
/// not an `ID3D11Texture2D` fails the QueryInterface probe cleanly.
pub unsafe fn adapter_of_probe(texture: *const c_void) -> Result<u64> {
    ensure!(!texture.is_null(), "texture is null");

    let raw = texture.cast_mut();
    let unknown = unsafe { IUnknown::from_raw_borrowed(&raw) }.context("texture is null")?;
    let texture: ID3D11Texture2D = unknown
        .cast()
        .context("probe texture is not an ID3D11Texture2D")?;

    let device: ID3D11Device =
        unsafe { texture.GetDevice() }.context("probe texture has no device")?;

    let dxgi: IDXGIDevice = device.cast().context("device is not an IDXGIDevice")?;
    let adapter = unsafe { dxgi.GetAdapter() }.context("GetAdapter failed")?;
    let desc = unsafe { adapter.GetDesc() }.context("GetDesc failed")?;

    Ok((u64::from(desc.AdapterLuid.HighPart as u32) << 32) | u64::from(desc.AdapterLuid.LowPart))
}
