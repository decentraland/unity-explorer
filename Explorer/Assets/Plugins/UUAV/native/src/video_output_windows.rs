use anyhow::{Result, anyhow, ensure};
use std::os::raw::c_void;
use windows::Win32::Graphics::Direct3D11::{
    D3D11_BIND_SHADER_RESOURCE, D3D11_BOX, D3D11_TEXTURE2D_DESC, D3D11_USAGE_DEFAULT, ID3D11Device,
    ID3D11DeviceContext, ID3D11Resource, ID3D11Texture2D,
};
use windows::Win32::Graphics::Dxgi::Common::{
    DXGI_FORMAT, DXGI_FORMAT_NV12, DXGI_FORMAT_P010, DXGI_SAMPLE_DESC,
};
use windows::core::Interface;

use crate::frame_info::FrameInfo;
use crate::hw_device::{HwDevice, HwDeviceContext};
use crate::video_decoder::VideoFrame;

pub(crate) struct VideoTextureView {
    texture: ID3D11Texture2D,
}

impl VideoTextureView {
    pub(crate) fn raw_ptr_mut(&self) -> *mut c_void {
        self.texture.as_raw()
    }
}

pub(crate) struct VideoOutput {
    device: ID3D11Device,
    texture: Option<SizedTexture>,
    info: Option<FrameInfo>,
    frames: u64,
    generation: u64,
}

struct SizedTexture {
    texture: ID3D11Texture2D,
    width: u32,
    height: u32,
    format: DXGI_FORMAT,
}

impl SizedTexture {
    fn matches(&self, width: u32, height: u32, format: DXGI_FORMAT) -> bool {
        self.width == width && self.height == height && self.format == format
    }
}

unsafe impl Send for VideoOutput {}

impl VideoOutput {
    pub(crate) fn new(device: &HwDevice) -> Self {
        Self {
            device: device.device().clone(),
            texture: None,
            info: None,
            frames: 0,
            generation: 0,
        }
    }

    pub(crate) fn texture(&self) -> Option<VideoTextureView> {
        self.texture.as_ref().map(|sized| VideoTextureView {
            texture: sized.texture.clone(),
        })
    }

    pub(crate) fn state(&self) -> Option<FrameInfo> {
        let sized = self.texture.as_ref()?;
        let mut info = self.info?;
        let raw = sized.texture.as_raw() as usize;
        info.planes = [raw, raw];
        Some(info)
    }

    pub(crate) fn present(&mut self, hw: &HwDeviceContext, frame: &VideoFrame) -> Result<()> {
        let mut info = frame.info();
        let width = info.visible_width & !1;
        let height = info.visible_height & !1;
        if width == 0 || height == 0 {
            return Err(anyhow!("frame has no presentable size"));
        }
        info.visible_width = width;
        info.visible_height = height;

        let format = if info.bit_depth > 8 {
            DXGI_FORMAT_P010
        } else {
            DXGI_FORMAT_NV12
        };
        let texture = self.ensure_texture(width, height, format)?;

        let ctx_raw = hw.immediate_context_raw();
        let ctx = unsafe { ID3D11DeviceContext::from_raw_borrowed(&ctx_raw) }
            .ok_or_else(|| anyhow!("hw device context has no immediate context"))?;

        if let Some(nv12) = frame.software_nv12() {
            ensure!(
                nv12.width() == width && nv12.height() == height,
                "software frame is {}x{}, texture is {width}x{height}",
                nv12.width(),
                nv12.height()
            );

            let _guard = hw.lock();
            unsafe {
                ctx.UpdateSubresource(
                    &texture,
                    0,
                    None,
                    nv12.bytes().as_ptr().cast::<c_void>(),
                    nv12.stride(),
                    0,
                );
            }
        } else {
            let src_raw = frame.texture_raw();
            let src = unsafe { ID3D11Resource::from_raw_borrowed(&src_raw) }
                .ok_or_else(|| anyhow!("decoded frame has no D3D11 texture"))?;

            let src_box = D3D11_BOX {
                left: 0,
                top: 0,
                front: 0,
                right: width,
                bottom: height,
                back: 1,
            };

            let _guard = hw.lock();
            unsafe {
                ctx.CopySubresourceRegion(
                    &texture,
                    0,
                    0,
                    0,
                    0,
                    src,
                    frame.subresource(),
                    Some(&src_box),
                );
            }
        }

        info.fit_planes([(width, height), (width / 2, height / 2)]);
        self.frames = self.frames.wrapping_add(1);
        info.frame_index = self.frames;
        info.surface_generation = self.generation;
        self.info = Some(info);
        Ok(())
    }

    fn ensure_texture(
        &mut self,
        width: u32,
        height: u32,
        format: DXGI_FORMAT,
    ) -> Result<ID3D11Texture2D> {
        let texture = match self.texture.take() {
            Some(existing) if existing.matches(width, height, format) => existing,
            _ => {
                self.generation = self.generation.wrapping_add(1);
                let desc = D3D11_TEXTURE2D_DESC {
                    Width: width,
                    Height: height,
                    MipLevels: 1,
                    ArraySize: 1,
                    Format: format,
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
                unsafe { self.device.CreateTexture2D(&desc, None, Some(&mut created)) }.map_err(
                    |e| anyhow!("CreateTexture2D({format:?} {width}x{height}) failed: {e}"),
                )?;

                let created = created.ok_or_else(|| {
                    anyhow!("CreateTexture2D({format:?} {width}x{height}) returned no texture")
                })?;
                SizedTexture {
                    texture: created,
                    width,
                    height,
                    format,
                }
            }
        };
        Ok(self.texture.insert(texture).texture.clone())
    }
}
