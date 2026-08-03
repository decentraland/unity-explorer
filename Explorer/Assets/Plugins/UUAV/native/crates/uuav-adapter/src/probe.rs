
use std::os::raw::c_void;

use anyhow::Result;

pub use platform::Probe;

#[cfg(target_os = "macos")]
mod platform {
    use super::{Result, c_void};

    use anyhow::anyhow;
    use objc2::rc::Retained;
    use objc2::runtime::ProtocolObject;
    use objc2_metal::{
        MTLCreateSystemDefaultDevice, MTLDevice, MTLPixelFormat, MTLResource as _, MTLTexture,
        MTLTextureDescriptor, MTLTextureUsage,
    };

    pub struct Probe {
        texture: Retained<ProtocolObject<dyn MTLTexture>>,
    }

    impl Probe {
        pub fn create(_luid: Option<u64>) -> Result<Self> {
            let device = MTLCreateSystemDefaultDevice()
                .ok_or_else(|| anyhow!("MTLCreateSystemDefaultDevice returned nil"))?;
            let descriptor = unsafe {
                MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
                    MTLPixelFormat::RGBA8Unorm,
                    1,
                    1,
                    false,
                )
            };
            descriptor.setUsage(MTLTextureUsage::ShaderRead);
            let texture = device
                .newTextureWithDescriptor(&descriptor)
                .ok_or_else(|| anyhow!("newTextureWithDescriptor returned nil"))?;
            Ok(Self { texture })
        }

        pub fn as_ptr(&self) -> *const c_void {
            Retained::as_ptr(&self.texture).cast()
        }

        pub fn describe(&self) -> String {
            format!("its own MTLDevice ({})", self.texture.device().name())
        }
    }
}

#[cfg(windows)]
mod platform {
    use super::{Result, c_void};

    use anyhow::{Context as _, anyhow};
    use uuav_ipc::win::gpu::HelperDevice;
    use windows::Win32::Graphics::Direct3D11::{
        D3D11_BIND_SHADER_RESOURCE, D3D11_TEXTURE2D_DESC, D3D11_USAGE_DEFAULT, ID3D11Texture2D,
    };
    use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_SAMPLE_DESC};
    use windows::core::Interface as _;

    pub struct Probe {
        texture: ID3D11Texture2D,
        _device: HelperDevice,
        luid: u64,
    }

    impl Probe {
        pub fn create(luid: Option<u64>) -> Result<Self> {
            let luid = luid.ok_or_else(|| {
                anyhow!(
                    "no engine adapter LUID was published; the decode device has to be created on \
                     the adapter the client's device is on, and there is no safe default"
                )
            })?;
            let device = HelperDevice::for_luid(luid)?;

            let descriptor = D3D11_TEXTURE2D_DESC {
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
            let mut texture: Option<ID3D11Texture2D> = None;
            unsafe {
                device
                    .device()
                    .CreateTexture2D(&descriptor, None, Some(&mut texture))
            }
            .context("CreateTexture2D for the probe")?;
            let texture =
                texture.ok_or_else(|| anyhow!("CreateTexture2D produced no probe texture"))?;

            Ok(Self {
                texture,
                _device: device,
                luid,
            })
        }

        pub fn as_ptr(&self) -> *const c_void {
            self.texture.as_raw().cast_const()
        }

        pub fn describe(&self) -> String {
            format!("a D3D11 device on adapter LUID {:#018x}", self.luid)
        }
    }
}
