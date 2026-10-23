//! Output buffers the camera renders into and the host composites from (plan § 8 "Output sharing").

#[cfg(target_os = "macos")]
pub mod iosurface;
pub mod offscreen;

use abi::{BmOutputKind, BmOutputTexture};
use bevy::{
    math::UVec2,
    render::{
        camera::ManualTextureView,
        render_resource::{Texture, TextureFormat, TextureView},
        renderer::RenderDevice,
    },
};
use std::ffi::c_void;

/// BGRA8 sRGB: what both `Texture2D.CreateExternalTexture(BGRA32, linear: false)` and Bevy's camera
/// output agree on.
pub const OUTPUT_FORMAT: TextureFormat = TextureFormat::Bgra8UnormSrgb;

/// One output buffer: the texture Bevy renders into plus whatever keeps the host's view of it alive.
pub struct OutputBuffer {
    pub texture: Texture,
    pub view: TextureView,
    pub host_texture: *mut c_void,
    pub io_surface: *mut c_void,
    /// Retained host-side objects; dropped with the buffer.
    _backing: Box<dyn std::any::Any + Send>,
}

impl OutputBuffer {
    pub fn manual_view(&self, size: UVec2) -> ManualTextureView {
        ManualTextureView {
            texture_view: self.view.clone(),
            size,
            format: OUTPUT_FORMAT,
        }
    }
}

pub struct OutputBuffers {
    pub buffers: Vec<OutputBuffer>,
    pub size: UVec2,
}

// SAFETY: the raw pointers are opaque handles the Bevy thread never dereferences; the retained
// objects behind them are released on the thread that drops the buffers.
unsafe impl Send for OutputBuffers {}

impl OutputBuffers {
    pub fn handles(&self) -> Vec<OutputHandles> {
        self.buffers
            .iter()
            .map(|buffer| OutputHandles {
                width: self.size.x,
                height: self.size.y,
                host_texture: buffer.host_texture,
                io_surface: buffer.io_surface,
            })
            .collect()
    }
}

/// What the host gets to see of one buffer; plain data so it can cross to the host thread.
#[derive(Debug, Clone, Copy)]
pub struct OutputHandles {
    pub width: u32,
    pub height: u32,
    pub host_texture: *mut c_void,
    pub io_surface: *mut c_void,
}

// SAFETY: opaque handles, see OutputBuffers.
unsafe impl Send for OutputHandles {}

impl OutputHandles {
    pub fn describe(&self) -> BmOutputTexture {
        BmOutputTexture {
            width: self.width,
            height: self.height,
            host_texture: self.host_texture,
            io_surface: self.io_surface,
        }
    }
}

pub fn create(
    kind: BmOutputKind,
    device: &RenderDevice,
    size: UVec2,
    count: u32,
) -> Result<OutputBuffers, String> {
    let mut buffers = Vec::with_capacity(count as usize);

    for index in 0..count {
        let buffer = match kind {
            BmOutputKind::Offscreen => offscreen::create(device, size, index),
            #[cfg(target_os = "macos")]
            BmOutputKind::IoSurface => iosurface::create(device, size, index)?,
            #[cfg(not(target_os = "macos"))]
            BmOutputKind::IoSurface => {
                return Err("unsupported: IOSurface output exists on macOS only".to_owned());
            }
        };

        buffers.push(buffer);
    }

    Ok(OutputBuffers { buffers, size })
}

pub fn texture_descriptor(size: UVec2, label: &str) -> wgpu::TextureDescriptor<'_> {
    wgpu::TextureDescriptor {
        label: Some(label),
        size: wgpu::Extent3d {
            width: size.x,
            height: size.y,
            depth_or_array_layers: 1,
        },
        mip_level_count: 1,
        sample_count: 1,
        dimension: wgpu::TextureDimension::D2,
        format: OUTPUT_FORMAT,
        usage: wgpu::TextureUsages::RENDER_ATTACHMENT
            | wgpu::TextureUsages::COPY_SRC
            | wgpu::TextureUsages::TEXTURE_BINDING,
        view_formats: &[],
    }
}
