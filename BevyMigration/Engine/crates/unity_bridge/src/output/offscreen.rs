//! Plain device textures: nothing is shared, pixels reach the host through `bm_read_output`.

use super::{OutputBuffer, texture_descriptor};
use bevy::{
    math::UVec2,
    render::{
        render_resource::{Texture, TextureView},
        renderer::RenderDevice,
    },
};

pub fn create(device: &RenderDevice, size: UVec2, index: u32) -> OutputBuffer {
    let label = format!("bevy-migration offscreen output {index}");
    let texture = device
        .wgpu_device()
        .create_texture(&texture_descriptor(size, &label));
    let view = texture.create_view(&wgpu::TextureViewDescriptor::default());

    OutputBuffer {
        texture: Texture::from(texture),
        view: TextureView::from(view),
        host_texture: std::ptr::null_mut(),
        io_surface: std::ptr::null_mut(),
        _backing: Box::new(()),
    }
}
