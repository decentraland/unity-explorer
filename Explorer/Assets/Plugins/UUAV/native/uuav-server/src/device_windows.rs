//! The helper's own D3D11 device plus the 1x1 probe texture handed to the
//! core's `uuav_init`, which derives its device from a texture pointer —
//! the exact contract Unity fulfills in-process today.

use anyhow::{Context as _, Result, bail};
use std::os::raw::c_void;
use windows::Win32::Graphics::Direct3D::D3D_DRIVER_TYPE_HARDWARE;
use windows::Win32::Graphics::Direct3D::D3D_DRIVER_TYPE_UNKNOWN;
use windows::Win32::Graphics::Direct3D11::{
    D3D11_BIND_SHADER_RESOURCE, D3D11_CREATE_DEVICE_FLAG, D3D11_SDK_VERSION,
    D3D11_TEXTURE2D_DESC, D3D11_USAGE_DEFAULT, D3D11CreateDevice, ID3D11Device, ID3D11Texture2D,
};
use windows::Win32::Foundation::HMODULE;
use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_SAMPLE_DESC};
use windows::Win32::Graphics::Dxgi::{CreateDXGIFactory1, IDXGIAdapter, IDXGIFactory1};
use windows::core::Interface as _;

pub struct ProbeDevice {
    probe: ID3D11Texture2D,
    #[allow(dead_code)] // retained so the device outlives the core's borrow of it
    device: ID3D11Device,
}

impl ProbeDevice {
    /// `adapter` is the packed LUID of Unity's adapter (0 = default). Decoding
    /// must happen on the same adapter as Unity or the shared textures cannot
    /// be opened cross-process, so a missing match is an error.
    pub fn new(adapter: u64) -> Result<Self> {
        let device = create_device(adapter)?;

        let desc = D3D11_TEXTURE2D_DESC {
            Width: 1,
            Height: 1,
            MipLevels: 1,
            ArraySize: 1,
            Format: DXGI_FORMAT_R8G8B8A8_UNORM,
            SampleDesc: DXGI_SAMPLE_DESC {
                Count: 1,
                Quality: 0,
            },
            Usage: D3D11_USAGE_DEFAULT,
            BindFlags: D3D11_BIND_SHADER_RESOURCE.0 as u32,
            CPUAccessFlags: 0,
            MiscFlags: 0,
        };
        let mut probe: Option<ID3D11Texture2D> = None;
        unsafe { device.CreateTexture2D(&desc, None, Some(&mut probe)) }
            .context("failed to create probe texture")?;
        let probe = probe.context("CreateTexture2D returned no texture")?;

        Ok(Self { probe, device })
    }

    /// The `ID3D11Texture2D*` pointer for the core's `uuav_init`.
    pub fn probe_ptr(&self) -> *const c_void {
        self.probe.as_raw().cast_const()
    }
}

fn create_device(adapter: u64) -> Result<ID3D11Device> {
    let (dxgi_adapter, driver_type): (Option<IDXGIAdapter>, _) = if adapter == 0 {
        (None, D3D_DRIVER_TYPE_HARDWARE)
    } else {
        (Some(find_adapter(adapter)?), D3D_DRIVER_TYPE_UNKNOWN)
    };

    let mut device: Option<ID3D11Device> = None;
    unsafe {
        D3D11CreateDevice(
            dxgi_adapter.as_ref(),
            driver_type,
            HMODULE::default(),
            D3D11_CREATE_DEVICE_FLAG(0),
            None,
            D3D11_SDK_VERSION,
            Some(&mut device),
            None,
            None,
        )
    }
    .context("D3D11CreateDevice failed")?;
    device.context("D3D11CreateDevice returned no device")
}

/// LUID packed as `(HighPart as u32 as u64) << 32 | LowPart` on both sides.
fn find_adapter(luid: u64) -> Result<IDXGIAdapter> {
    let factory: IDXGIFactory1 = unsafe { CreateDXGIFactory1() }.context("CreateDXGIFactory1")?;
    for index in 0.. {
        let Ok(candidate) = (unsafe { factory.EnumAdapters1(index) }) else {
            break;
        };
        let desc = unsafe { candidate.GetDesc1() }.context("GetDesc1")?;
        let packed = (u64::from(desc.AdapterLuid.HighPart.cast_unsigned()) << 32)
            | u64::from(desc.AdapterLuid.LowPart);
        if packed == luid {
            return candidate.cast().context("IDXGIAdapter1 -> IDXGIAdapter");
        }
    }
    bail!("no DXGI adapter matches Unity's LUID {luid}");
}
