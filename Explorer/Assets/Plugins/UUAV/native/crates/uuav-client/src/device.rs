
use std::os::raw::c_void;

use uuav_abi::errors;

#[cfg(target_os = "macos")]
use objc2::rc::Retained;
#[cfg(target_os = "macos")]
use objc2::runtime::ProtocolObject;
#[cfg(target_os = "macos")]
use objc2_metal::{MTLDevice, MTLResource as _, MTLTexture};

#[cfg(windows)]
use windows::Win32::Graphics::Direct3D11::{ID3D11Device, ID3D11Texture2D};
#[cfg(windows)]
use windows::core::{IUnknown, Interface as _};

pub struct EngineDevice {
    #[cfg(windows)]
    device: ID3D11Device,
    #[cfg(target_os = "macos")]
    _device: Retained<ProtocolObject<dyn MTLDevice>>,
}

unsafe impl Send for EngineDevice {}
unsafe impl Sync for EngineDevice {}

impl EngineDevice {
    #[cfg(windows)]
    pub unsafe fn from_probe(texture: *const c_void) -> Result<Self, String> {
        let raw = texture.cast_mut();
        let unknown = unsafe { IUnknown::from_raw_borrowed(&raw) }
            .ok_or_else(|| errors::TEXTURE_NOT_COM.to_owned())?;
        let probe: ID3D11Texture2D = unknown
            .cast()
            .map_err(|_| errors::TEXTURE_NOT_D3D11.to_owned())?;
        let device = unsafe { probe.GetDevice() }
            .map_err(|error| format!("cannot read the device off the texture: {error}"))?;
        Ok(Self { device })
    }

    #[cfg(target_os = "macos")]
    pub unsafe fn from_probe(texture: *const c_void) -> Result<Self, String> {
        if texture.is_null() {
            return Err(errors::NO_TEXTURE.to_owned());
        }
        let probe: &ProtocolObject<dyn MTLTexture> = unsafe { &*texture.cast() };
        Ok(Self {
            _device: probe.device(),
        })
    }

    #[cfg(windows)]
    pub fn luid(&self) -> Option<u64> {
        uuav_ipc::win::gpu::adapter_luid(&self.device)
            .ok()
            .filter(|luid| *luid != 0)
    }

    #[cfg(target_os = "macos")]
    pub const fn luid(&self) -> Option<u64> {
        None
    }

    #[cfg(windows)]
    pub fn d3d11(&self) -> ID3D11Device {
        self.device.clone()
    }

    #[cfg(windows)]
    pub fn removed_reason(&self) -> Option<String> {
        unsafe { self.device.GetDeviceRemovedReason() }
            .err()
            .map(|error| error.to_string())
    }

    #[cfg(target_os = "macos")]
    pub const fn removed_reason(&self) -> Option<String> {
        None
    }
}
