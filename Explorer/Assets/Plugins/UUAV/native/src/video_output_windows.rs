use anyhow::{Result, anyhow};
use std::os::raw::c_void;
use windows::Win32::Graphics::Direct3D11::{
    D3D11_BIND_SHADER_RESOURCE, D3D11_BOX, D3D11_TEXTURE2D_DESC, D3D11_USAGE_DEFAULT, ID3D11Device,
    ID3D11DeviceContext, ID3D11Resource, ID3D11Texture2D,
};
use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_NV12, DXGI_SAMPLE_DESC};
use windows::core::Interface;

use crate::hw_device::{HwDevice, HwDeviceContext};
use crate::video_decoder::VideoFrame;

/// Non-owning view of [`VideoOutput`]'s presentation texture: one NV12
/// `ID3D11Texture2D` covering both planes, whatever plane was asked for -
/// the engine creates its own format-cast views over it (R8 -> Y,
/// R8G8 -> UV). The COM reference held here only pins the resource while
/// the view lives; `VideoOutput` stays the owner and recreates the texture
/// on resolution change, after which consumers re-query (contract of
/// `uuav_player_get_video_texture`).
pub(crate) struct VideoTextureView {
    texture: ID3D11Texture2D,
}

impl VideoTextureView {
    pub(crate) fn raw_ptr_mut(&self) -> *mut c_void {
        self.texture.as_raw()
    }
}

/// Presentation target: an NV12 texture on the engine-provided device that
/// decoded frames are copied into on the render thread. The engine consumes
/// the raw `ID3D11Texture2D*` and creates its own views over the planes.
pub(crate) struct VideoOutput {
    device: ID3D11Device,
    texture: Option<SizedTexture>,
}

/// The NV12 texture together with the dimensions it was created for.
struct SizedTexture {
    texture: ID3D11Texture2D,
    width: u32,
    height: u32,
}

impl SizedTexture {
    const fn matches(&self, width: u32, height: u32) -> bool {
        self.width == width && self.height == height
    }
}

// The COM pointers are only used behind the player's mutex and D3D11
// devices are free-threaded.
unsafe impl Send for VideoOutput {}

impl VideoOutput {
    pub(crate) fn new(device: &HwDevice) -> Self {
        Self {
            device: device.device().clone(),
            texture: None,
        }
    }

    /// The shared NV12 texture, once the first frame has been presented.
    pub(crate) fn texture(&self) -> Option<VideoTextureView> {
        self.texture.as_ref().map(|sized| VideoTextureView {
            texture: sized.texture.clone(),
        })
    }

    pub(crate) fn present(&mut self, hw: &HwDeviceContext, frame: &VideoFrame) -> Result<()> {
        // NV12 requires even dimensions; negative decoder sizes fold to 0
        let width = u32::try_from(frame.width()).unwrap_or(0) & !1;
        let height = u32::try_from(frame.height()).unwrap_or(0) & !1;
        if width == 0 || height == 0 {
            return Err(anyhow!("frame has no presentable size"));
        }
        let texture = self.ensure_texture(width, height)?;

        let src_raw = frame.texture_raw();
        let src = unsafe { ID3D11Resource::from_raw_borrowed(&src_raw) }
            .ok_or_else(|| anyhow!("decoded frame has no D3D11 texture"))?;

        let ctx_raw = hw.immediate_context_raw();
        let ctx = unsafe { ID3D11DeviceContext::from_raw_borrowed(&ctx_raw) }
            .ok_or_else(|| anyhow!("hw device context has no immediate context"))?;

        // The decoder surface can be part of a larger texture array with
        // padded (coded-size) dimensions; copy just the visible box.
        let src_box = D3D11_BOX {
            left: 0,
            top: 0,
            front: 0,
            right: width,
            bottom: height,
            back: 1,
        };

        // FFmpeg's own decoder threads drive the same immediate context;
        // its device lock serializes us against them.
        let _guard = hw.lock();
        unsafe {
            ctx.CopySubresourceRegion(
                texture,
                0,
                0,
                0,
                0,
                src,
                frame.subresource(),
                Some(&src_box),
            );
        }
        Ok(())
    }

    /// Returns the presentation texture, recreating it when the size
    /// changed or none exists yet.
    fn ensure_texture(&mut self, width: u32, height: u32) -> Result<&ID3D11Texture2D> {
        let texture = match self.texture.take() {
            Some(existing) if existing.matches(width, height) => existing,
            // TODO push a resolution-change notification to the consumer;
            // for now C# polls texture ptr + size each frame (UUAVPlayer)
            // and re-wraps after the pointer changes
            _ => {
                let desc = D3D11_TEXTURE2D_DESC {
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
                    MiscFlags: 0,
                };

                let mut created: Option<ID3D11Texture2D> = None;
                unsafe { self.device.CreateTexture2D(&desc, None, Some(&mut created)) }
                    .map_err(|e| anyhow!("CreateTexture2D(NV12 {width}x{height}) failed: {e}"))?;

                let created = created.ok_or_else(|| {
                    anyhow!("CreateTexture2D(NV12 {width}x{height}) returned no texture")
                })?;
                SizedTexture {
                    texture: created,
                    width,
                    height,
                }
            }
        };
        Ok(&self.texture.insert(texture).texture)
    }
}
