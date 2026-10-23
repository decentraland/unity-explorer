//! Copies the rendered output into a mappable buffer and hands the pixels back on the main world:
//! the offscreen path of `bm_read_output`, and what the step 3 screenshot harness will diff.
//!
//! Follows Bevy's `headless_renderer` example: a render-graph node after the cameras copies the
//! output texture into a buffer, a cleanup system maps it and ships the bytes over a channel.

use bevy::{
    image::TextureFormatPixelInfo,
    prelude::*,
    render::{
        Render, RenderApp, RenderSet,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_graph::{self, NodeRunError, RenderGraph, RenderGraphContext, RenderLabel},
        render_resource::{
            Buffer, BufferDescriptor, BufferUsages, Extent3d, Maintain, MapMode, Texture,
            TextureFormat,
        },
        renderer::{RenderContext, RenderDevice},
    },
};
use parking_lot::Mutex;
use std::sync::{Arc, mpsc};

/// The texture to read back this frame and its size; replaced by the embedder together with the
/// output view it points the camera at.
#[derive(Resource, Debug, Clone, ExtractResource)]
pub struct ReadbackSource {
    pub texture: Texture,
    pub size: UVec2,
    pub format: TextureFormat,
}

/// Latest pixels, tightly packed rows, in the output texture's format.
#[derive(Resource, Debug, Default, Clone)]
pub struct LatestPixels(pub Arc<Mutex<Option<Pixels>>>);

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Pixels {
    pub size: UVec2,
    pub bytes_per_pixel: u32,
    pub data: Vec<u8>,
}

#[derive(Resource)]
struct ReadbackBuffer {
    buffer: Buffer,
    padded_bytes_per_row: u32,
    size: UVec2,
}

#[derive(Resource)]
struct PixelSender(mpsc::Sender<Pixels>);

#[derive(Resource, Debug)]
pub struct PixelReceiver(Mutex<mpsc::Receiver<Pixels>>);

#[derive(Debug, Hash, PartialEq, Eq, Clone, RenderLabel)]
struct ReadbackLabel;

#[derive(Debug, Default)]
pub struct ReadbackPlugin;

impl Plugin for ReadbackPlugin {
    fn build(&self, app: &mut App) {
        let (tx, rx) = mpsc::channel();
        app.add_plugins(ExtractResourcePlugin::<ReadbackSource>::default())
            .init_resource::<LatestPixels>()
            .insert_resource(PixelReceiver(Mutex::new(rx)))
            .add_systems(PostUpdate, receive_pixels);

        let render_app = app.sub_app_mut(RenderApp);
        render_app
            .insert_resource(PixelSender(tx))
            .add_systems(Render, prepare_buffer.in_set(RenderSet::PrepareResources))
            .add_systems(Render, map_and_send.in_set(RenderSet::Cleanup));

        let mut graph = render_app.world_mut().resource_mut::<RenderGraph>();
        graph.add_node(ReadbackLabel, ReadbackNode);
        graph.add_node_edge(bevy::render::graph::CameraDriverLabel, ReadbackLabel);
    }
}

fn prepare_buffer(
    mut commands: Commands,
    device: Res<RenderDevice>,
    source: Option<Res<ReadbackSource>>,
    existing: Option<Res<ReadbackBuffer>>,
) {
    let Some(source) = source else {
        return;
    };

    if existing.is_some_and(|buffer| buffer.size == source.size) {
        return;
    }

    let bytes_per_pixel = u32::try_from(source.format.pixel_size()).unwrap_or(4);
    let unpadded = source.size.x * bytes_per_pixel;
    let align = wgpu::COPY_BYTES_PER_ROW_ALIGNMENT;
    let padded_bytes_per_row = unpadded.div_ceil(align) * align;

    let buffer = device.create_buffer(&BufferDescriptor {
        label: Some("bevy-migration readback"),
        size: u64::from(padded_bytes_per_row) * u64::from(source.size.y),
        usage: BufferUsages::MAP_READ | BufferUsages::COPY_DST,
        mapped_at_creation: false,
    });

    commands.insert_resource(ReadbackBuffer {
        buffer,
        padded_bytes_per_row,
        size: source.size,
    });
}

struct ReadbackNode;

impl render_graph::Node for ReadbackNode {
    fn run(
        &self,
        _graph: &mut RenderGraphContext,
        render_context: &mut RenderContext,
        world: &World,
    ) -> Result<(), NodeRunError> {
        let (Some(source), Some(target)) = (
            world.get_resource::<ReadbackSource>(),
            world.get_resource::<ReadbackBuffer>(),
        ) else {
            return Ok(());
        };

        let encoder = render_context.command_encoder();
        encoder.copy_texture_to_buffer(
            source.texture.as_image_copy(),
            wgpu::TexelCopyBufferInfo {
                buffer: &target.buffer,
                layout: wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(target.padded_bytes_per_row),
                    rows_per_image: None,
                },
            },
            Extent3d {
                width: source.size.x,
                height: source.size.y,
                depth_or_array_layers: 1,
            },
        );

        Ok(())
    }
}

fn map_and_send(
    device: Res<RenderDevice>,
    target: Option<Res<ReadbackBuffer>>,
    source: Option<Res<ReadbackSource>>,
    sender: Res<PixelSender>,
) {
    let (Some(target), Some(source)) = (target, source) else {
        return;
    };

    let slice = target.buffer.slice(..);
    let (tx, rx) = mpsc::channel();
    slice.map_async(MapMode::Read, move |result| {
        let _ = tx.send(result);
    });
    let _ = device.poll(Maintain::Wait);

    let Ok(Ok(())) = rx.recv() else {
        return;
    };

    let bytes_per_pixel = u32::try_from(source.format.pixel_size()).unwrap_or(4);
    let unpadded = (target.size.x * bytes_per_pixel) as usize;
    let padded = target.padded_bytes_per_row as usize;
    let mapped = slice.get_mapped_range();
    let mut data = Vec::with_capacity(unpadded * target.size.y as usize);

    for row in mapped.chunks(padded) {
        data.extend_from_slice(&row[..unpadded.min(row.len())]);
    }

    drop(mapped);
    target.buffer.unmap();
    let _ = sender.0.send(Pixels {
        size: target.size,
        bytes_per_pixel,
        data,
    });
}

/// Moves pixels that arrived from the render world into [`LatestPixels`]. Runs every frame in
/// `PostUpdate`, which precedes the render of that same frame, so an embedder that wants the frame it
/// just rendered calls this right after `App::update`.
pub fn collect_latest(world: &mut World) {
    let newest = world
        .resource::<PixelReceiver>()
        .0
        .try_lock()
        .and_then(|receiver| {
            let mut newest = None;

            while let Ok(pixels) = receiver.try_recv() {
                newest = Some(pixels);
            }

            newest
        });

    if let Some(pixels) = newest {
        *world.resource::<LatestPixels>().0.lock() = Some(pixels);
    }
}

fn receive_pixels(receiver: Res<PixelReceiver>, latest: Res<LatestPixels>) {
    let receiver = receiver.0.lock();
    let mut newest = None;

    while let Ok(pixels) = receiver.try_recv() {
        newest = Some(pixels);
    }

    if let Some(pixels) = newest {
        *latest.0.lock() = Some(pixels);
    }
}
