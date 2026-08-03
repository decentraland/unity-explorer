
use std::os::windows::io::{FromRawHandle as _, OwnedHandle};

use anyhow::{Context as _, Result, anyhow, bail};
use windows::Win32::Graphics::Direct3D::D3D_FEATURE_LEVEL_11_0;
use windows::Win32::Graphics::Direct3D11::{
    D3D11_BIND_SHADER_RESOURCE, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
    D3D11_CREATE_DEVICE_VIDEO_SUPPORT, D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX,
    D3D11_RESOURCE_MISC_SHARED_NTHANDLE, D3D11_SDK_VERSION, D3D11_TEXTURE2D_DESC,
    D3D11_USAGE_DEFAULT, D3D11CreateDevice, ID3D11Device, ID3D11Device1, ID3D11Texture2D,
};
use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_NV12, DXGI_SAMPLE_DESC};
use windows::Win32::Graphics::Dxgi::{
    CreateDXGIFactory1, DXGI_ERROR_NOT_FOUND, DXGI_SHARED_RESOURCE_READ, DXGI_SHARED_RESOURCE_WRITE,
    IDXGIAdapter1, IDXGIDevice, IDXGIFactory1, IDXGIKeyedMutex, IDXGIResource1,
};
use windows::core::{HRESULT, Interface};

use crate::protocol::{self, Fault, SurfaceGeometry};

pub const SHARED_RESOURCE_ACCESS: u32 =
    DXGI_SHARED_RESOURCE_READ.0 | DXGI_SHARED_RESOURCE_WRITE.0;

pub const KEY: u64 = 0;

const WAIT_ABANDONED_HR: HRESULT = HRESULT(0x0000_0080);
const WAIT_TIMEOUT_HR: HRESULT = HRESULT(0x0000_0102);

const _: () = assert!(WAIT_ABANDONED_HR.0 >= 0);
const _: () = assert!(WAIT_TIMEOUT_HR.0 >= 0);

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Acquired {
    Yes,
    Busy,
    Abandoned,
}

pub fn acquire(mutex: &IDXGIKeyedMutex, key: u64, timeout_ms: u32) -> Result<Acquired> {
    let hr =
        unsafe { (Interface::vtable(mutex).AcquireSync)(mutex.as_raw(), key, timeout_ms) };
    match hr {
        HRESULT(0) => Ok(Acquired::Yes),
        WAIT_TIMEOUT_HR => Ok(Acquired::Busy),
        WAIT_ABANDONED_HR => Ok(Acquired::Abandoned),
        other => bail!("IDXGIKeyedMutex::AcquireSync(key {key}) -> {:#010x}", other.0),
    }
}

pub fn release(mutex: &IDXGIKeyedMutex, key: u64) -> Result<()> {
    let hr = unsafe { (Interface::vtable(mutex).ReleaseSync)(mutex.as_raw(), key) };
    if hr.0 != 0 {
        bail!("IDXGIKeyedMutex::ReleaseSync(key {key}) -> {:#010x}", hr.0);
    }
    Ok(())
}

pub fn with_key<T>(
    mutex: &IDXGIKeyedMutex,
    key: u64,
    to: u64,
    timeout_ms: u32,
    body: impl FnOnce() -> Result<T>,
) -> Result<Option<T>> {
    match acquire(mutex, key, timeout_ms)? {
        Acquired::Busy => Ok(None),
        Acquired::Abandoned => Err(Fault::Message {
            kind: protocol::kind::SURFACE,
            what: "the shared surface's keyed mutex was abandoned by a dead peer",
        }
        .into()),
        Acquired::Yes => {
            let outcome = body();
            let released = release(mutex, to);
            outcome.and_then(|value| released.map(|()| Some(value)))
        }
    }
}

pub fn adapter_luid(device: &ID3D11Device) -> Result<u64> {
    let dxgi: IDXGIDevice = device
        .cast()
        .context("the engine device does not expose IDXGIDevice")?;
    let adapter = unsafe { dxgi.GetAdapter() }.context("IDXGIDevice::GetAdapter")?;
    let description = unsafe { adapter.GetDesc() }.context("IDXGIAdapter::GetDesc")?;
    Ok(pack_luid(
        description.AdapterLuid.LowPart,
        description.AdapterLuid.HighPart,
    ))
}

pub const fn pack_luid(low: u32, high: i32) -> u64 {
    ((high as u32 as u64) << 32) | (low as u64)
}

#[allow(
    clippy::cast_possible_wrap,
    reason = "a LUID's HighPart is signed; reinterpreting the top 32 bits is the \
              inverse of how `pack_luid` stored them, not an accident"
)]
pub const fn unpack_luid(packed: u64) -> (u32, i32) {
    (packed as u32, (packed >> 32) as u32 as i32)
}

pub fn open_shared(device: &ID3D11Device, handle: &OwnedHandle) -> Result<ID3D11Texture2D> {
    let device1: ID3D11Device1 = device.cast().context(
        "the engine device does not expose ID3D11Device1; feature level 11.1 is required to open \
         an NT-handle shared resource",
    )?;
    let texture: ID3D11Texture2D = unsafe {
        device1.OpenSharedResource1(windows::Win32::Foundation::HANDLE(
            std::os::windows::io::AsRawHandle::as_raw_handle(handle).cast(),
        ))
    }
    .context("ID3D11Device1::OpenSharedResource1")?;
    Ok(texture)
}

pub fn measure(texture: &ID3D11Texture2D) -> Result<SurfaceGeometry, Fault> {
    let mut description = D3D11_TEXTURE2D_DESC::default();
    unsafe { texture.GetDesc(&raw mut description) };

    if description.Format != DXGI_FORMAT_NV12 {
        return Err(Fault::Message {
            kind: protocol::kind::SURFACE,
            what: "shared surface is not an NV12 texture",
        });
    }
    let width = plane_dimension(description.Width)?;
    let height = plane_dimension(description.Height)?;
    if !width.is_multiple_of(2) || !height.is_multiple_of(2) {
        return Err(Fault::Message {
            kind: protocol::kind::SURFACE,
            what: "NV12 surface has an odd dimension",
        });
    }
    Ok(SurfaceGeometry {
        plane_width: [width, width / 2],
        plane_height: [height, height / 2],
        plane_count: 2,
    })
}

const fn plane_dimension(value: u32) -> Result<u32, Fault> {
    if value == 0 || value > protocol::MAX_PLANE_DIMENSION {
        return Err(Fault::Message {
            kind: protocol::kind::SURFACE,
            what: "shared surface plane dimension out of range",
        });
    }
    Ok(value)
}

pub struct HelperDevice {
    device: ID3D11Device,
}

impl HelperDevice {
    pub fn for_luid(luid: u64) -> Result<Self> {
        let (low, high) = unpack_luid(luid);
        let factory: IDXGIFactory1 =
            unsafe { CreateDXGIFactory1() }.context("CreateDXGIFactory1")?;

        for index in 0u32.. {
            let adapter: IDXGIAdapter1 = match unsafe { factory.EnumAdapters1(index) } {
                Ok(adapter) => adapter,
                Err(error) if error.code() == DXGI_ERROR_NOT_FOUND => break,
                Err(error) => return Err(error).context("IDXGIFactory1::EnumAdapters1"),
            };
            let description =
                unsafe { adapter.GetDesc1() }.context("IDXGIAdapter1::GetDesc1")?;
            if description.AdapterLuid.LowPart != low || description.AdapterLuid.HighPart != high {
                continue;
            }

            let mut device: Option<ID3D11Device> = None;
            let levels = [D3D_FEATURE_LEVEL_11_0];
            unsafe {
                D3D11CreateDevice(
                    &adapter,
                    windows::Win32::Graphics::Direct3D::D3D_DRIVER_TYPE_UNKNOWN,
                    windows::Win32::Foundation::HMODULE::default(),
                    D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_VIDEO_SUPPORT,
                    Some(&levels),
                    D3D11_SDK_VERSION,
                    Some(&raw mut device),
                    None,
                    None,
                )
            }
            .context("D3D11CreateDevice on the engine's adapter")?;

            let device = device
                .ok_or_else(|| anyhow!("D3D11CreateDevice reported success but produced nothing"))?;
            return Ok(Self { device });
        }

        bail!("no adapter with LUID {luid:#018x}; the helper must take the software path")
    }

    pub fn adopt(device: ID3D11Device) -> Self {
        Self { device }
    }

    pub const fn device(&self) -> &ID3D11Device {
        &self.device
    }

    pub fn create_shared_nv12(&self, width: u32, height: u32) -> Result<SharedSurface> {
        if width == 0 || height == 0 || !width.is_multiple_of(2) || !height.is_multiple_of(2) {
            bail!("NV12 needs non-zero even dimensions, got {width}x{height}");
        }
        let description = D3D11_TEXTURE2D_DESC {
            Width: width,
            Height: height,
            MipLevels: 1,
            ArraySize: 1,
            Format: DXGI_FORMAT_NV12,
            SampleDesc: DXGI_SAMPLE_DESC {
                Count: 1,
                Quality: 0,
            },
            Usage: D3D11_USAGE_DEFAULT,
            BindFlags: D3D11_BIND_SHADER_RESOURCE.0 as u32,
            CPUAccessFlags: 0,
            MiscFlags: (D3D11_RESOURCE_MISC_SHARED_NTHANDLE.0
                | D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX.0) as u32,
        };

        let mut texture: Option<ID3D11Texture2D> = None;
        unsafe {
            self.device
                .CreateTexture2D(&description, None, Some(&raw mut texture))
        }
        .with_context(|| format!("CreateTexture2D(shared NV12 {width}x{height})"))?;
        let texture = texture
            .ok_or_else(|| anyhow!("CreateTexture2D reported success but produced nothing"))?;

        let resource: IDXGIResource1 = texture
            .cast()
            .context("shared texture does not expose IDXGIResource1")?;
        let raw = unsafe {
            resource.CreateSharedHandle(None, SHARED_RESOURCE_ACCESS, None)
        }
        .context("IDXGIResource1::CreateSharedHandle")?;
        let handle = unsafe { OwnedHandle::from_raw_handle(raw.0.cast()) };

        let mutex: IDXGIKeyedMutex = texture
            .cast()
            .context("shared texture does not expose IDXGIKeyedMutex")?;

        Ok(SharedSurface {
            texture,
            mutex,
            handle,
            width,
            height,
        })
    }
}

pub struct SharedSurface {
    texture: ID3D11Texture2D,
    mutex: IDXGIKeyedMutex,
    handle: OwnedHandle,
    width: u32,
    height: u32,
}

impl SharedSurface {
    pub const fn texture(&self) -> &ID3D11Texture2D {
        &self.texture
    }

    pub const fn mutex(&self) -> &IDXGIKeyedMutex {
        &self.mutex
    }

    pub fn handle_value(&self) -> u64 {
        std::os::windows::io::AsRawHandle::as_raw_handle(&self.handle) as usize as u64
    }

    pub const fn size(&self) -> (u32, u32) {
        (self.width, self.height)
    }
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              exists to keep them out of the shipped code paths"
)]
mod tests {
    use super::*;

    #[test]
    fn luid_packing_round_trips_including_a_negative_high_part() {
        for (low, high) in [(0u32, 0i32), (0x9c40, 1), (u32::MAX, -1), (1, i32::MIN)] {
            let packed = pack_luid(low, high);
            assert_eq!(unpack_luid(packed), (low, high), "{low:#x}/{high:#x}");
        }
        assert_eq!(pack_luid(0, 0), 0);
        assert_ne!(pack_luid(1, 0), 0);
        assert_ne!(pack_luid(0, 1), 0);
    }

    #[test]
    fn the_keyed_mutex_wait_codes_are_the_documented_ones() {
        assert_eq!(WAIT_TIMEOUT_HR.0, 0x102);
        assert_eq!(WAIT_ABANDONED_HR.0, 0x80);
    }
}
