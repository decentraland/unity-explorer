//! The video half of the adapter on Linux: drives the core's render
//! events, copies its presentation planes into cross-process shared
//! Vulkan images, and publishes frame availability to the client.
//!
//! Per player: a generation of 3 slots x 2 single-plane images (Y `R8`
//! at w x h, UV `R8G8` at w/2 x h/2), allocated exportable on the one
//! helper-wide device. The export flavor follows the client's graphics
//! API: dma-buf fds with a LINEAR DRM modifier for a Vulkan client,
//! opaque memory fds (optimal tiling) for a GL client. A generation is
//! announced once (fds tagged over the surface channel + `TextureSet`
//! over the control channel) and becomes active on `TextureSetAck`;
//! the generation state machine lives in `video_slots`. Slot copies are
//! fenced before `FramePublished` — the publish only ever announces
//! completed GPU work, which is the cross-process sync.

use crate::device::ProbeDevice;
use crate::state;
use crate::video_slots::{PlayerVideo, SLOTS};
use anyhow::{Context as _, Result, anyhow, bail, ensure};
use ash::vk;
use ash::vk::Handle as _;
use std::collections::HashMap;
use std::os::fd::{AsRawFd as _, FromRawFd as _, OwnedFd};
use std::os::raw::c_void;
use std::sync::Arc;
use uuav_core as core;
use uuav_core::linux_device::HeadlessDevice;
use uuav_ipc::channel::Channel;
use uuav_ipc::fd_channel;
use uuav_ipc::protocol::{
    GraphicsApiWire, PlayerId, TextureImportWire, TexturePlaneWire, ToClient,
};

/// `DRM_FORMAT_MOD_LINEAR`: the modifier the dma-buf slots are
/// allocated with. Universally importable; picking tiled modifiers from
/// the driver's list is deliberate headroom.
const MOD_LINEAR: u64 = 0;

#[derive(Clone, Copy, PartialEq, Eq)]
enum ExportMode {
    DmaBuf,
    OpaqueFd,
}

/// One exportable single-plane image plus its wire description; the fd
/// is consumed at announce (the kernel installs a duplicate in the
/// client).
struct ExportedImage {
    device: Arc<HeadlessDevice>,
    image: vk::Image,
    memory: vk::DeviceMemory,
    offset: u64,
    pitch: u32,
    size: u64,
    modifier: u64,
    fd: Option<OwnedFd>,
}

impl Drop for ExportedImage {
    fn drop(&mut self) {
        unsafe {
            let device = self.device.device();
            device.destroy_image(self.image, None);
            device.free_memory(self.memory, None);
        }
    }
}

pub struct SlotPair {
    y: ExportedImage,
    uv: ExportedImage,
}

pub struct VideoPump {
    device: Arc<HeadlessDevice>,
    surface: fd_channel::Sender,
    mode: ExportMode,
    pool: vk::CommandPool,
    buffer: vk::CommandBuffer,
    fence: vk::Fence,
    render_event: core::UUAVRenderEvent,
    players: HashMap<PlayerId, PlayerVideo<SlotPair>>,
}

impl Drop for VideoPump {
    fn drop(&mut self) {
        unsafe {
            let device = self.device.device();
            device.destroy_fence(self.fence, None);
            device.destroy_command_pool(self.pool, None);
        }
    }
}

impl VideoPump {
    pub fn new(
        probe: &ProbeDevice,
        graphics: GraphicsApiWire,
        surface: fd_channel::Sender,
    ) -> Result<Self> {
        let mode = match graphics {
            GraphicsApiWire::Vulkan => ExportMode::DmaBuf,
            GraphicsApiWire::OpenGl => ExportMode::OpaqueFd,
            GraphicsApiWire::Default => {
                bail!("Linux client did not report its graphics API")
            }
        };
        let device = Arc::clone(probe.headless());
        let vk_device = device.device();
        unsafe {
            let pool_info =
                vk::CommandPoolCreateInfo::default().queue_family_index(device.queue_family());
            let pool = vk_device
                .create_command_pool(&pool_info, None)
                .context("create pump command pool")?;
            let alloc = vk::CommandBufferAllocateInfo::default()
                .command_pool(pool)
                .level(vk::CommandBufferLevel::PRIMARY)
                .command_buffer_count(1);
            let buffer = match vk_device.allocate_command_buffers(&alloc) {
                Ok(buffers) => buffers.first().copied(),
                Err(e) => {
                    vk_device.destroy_command_pool(pool, None);
                    return Err(e).context("allocate pump command buffer");
                }
            };
            let Some(buffer) = buffer else {
                vk_device.destroy_command_pool(pool, None);
                bail!("no pump command buffer");
            };
            let fence = match vk_device.create_fence(&vk::FenceCreateInfo::default(), None) {
                Ok(fence) => fence,
                Err(e) => {
                    vk_device.destroy_command_pool(pool, None);
                    return Err(e).context("create pump fence");
                }
            };
            Ok(Self {
                device,
                surface,
                mode,
                pool,
                buffer,
                fence,
                render_event: core::uuav_get_render_callback(),
                players: HashMap::new(),
            })
        }
    }

    /// One video tick over all live players: render event, generation
    /// management, slot copy, publish.
    pub fn tick(&mut self, ids: impl Iterator<Item = PlayerId>, channel: &mut Channel) {
        for id in ids {
            if let Err(e) = self.tick_player(id, channel) {
                // per-player video hiccups are not protocol failures; the
                // helper's own logs are the diagnostic channel
                eprintln!("uuav-helper: video tick for player {id}: {e}");
            }
        }
    }

    pub fn ack(&mut self, id: PlayerId, generation: u32) {
        if let Some(player) = self.players.get_mut(&id) {
            player.ack(generation);
        }
    }

    pub fn remove_player(&mut self, id: PlayerId) {
        self.players.remove(&id);
    }

    fn tick_player(&mut self, id: PlayerId, channel: &mut Channel) -> Result<()> {
        // the core presents the due frame into its presentation planes,
        // exactly as it would for Unity's render thread
        (self.render_event)(id as i32);

        let Some((core_y, core_uv, width, height)) = query_core_planes(id) else {
            // no frame presented yet (opening, audio-only, closed)
            return Ok(());
        };

        // disjoint field borrows: the create closure needs the device,
        // mode and both channels while the player entry stays borrowed
        let device = &self.device;
        let mode = self.mode;
        let surface = &mut self.surface;
        let (pool, buffer, fence) = (self.pool, self.buffer, self.fence);
        let player = self.players.entry(id).or_default();

        player.ensure_generation(width, height, |generation| {
            let mut slots = create_generation(device, mode, pool, buffer, fence, width, height)?;
            announce(
                surface, channel, id, generation, mode, width, height, &mut slots,
            )?;
            Ok(slots)
        })?;

        let Some((slot, generation, slot_index)) = player.take_slot(width, height) else {
            return Ok(());
        };

        copy_planes(
            device, pool, buffer, fence, core_y, core_uv, slot, width, height,
        )?;

        channel.send(&ToClient::FramePublished {
            id,
            generation,
            slot: slot_index,
        })
    }
}

/// The core's presentation plane handles + visible size, or `None`
/// while unavailable (mirrors what a Vulkan engine's poll sees).
fn query_core_planes(id: PlayerId) -> Option<(vk::Image, vk::Image, u32, u32)> {
    let mut y: *const c_void = std::ptr::null();
    state::consume_result(unsafe { core::uuav_player_get_video_texture(id, 0, &mut y) }).ok()?;
    let mut uv: *const c_void = std::ptr::null();
    state::consume_result(unsafe { core::uuav_player_get_video_texture(id, 1, &mut uv) }).ok()?;

    let mut size = core::VideoSize {
        width: 0,
        height: 0,
    };
    state::consume_result(unsafe { core::uuav_player_get_video_size(id, &mut size) }).ok()?;
    if y.is_null() || uv.is_null() || size.width == 0 || size.height == 0 {
        return None;
    }
    Some((
        vk::Image::from_raw(y as u64),
        vk::Image::from_raw(uv as u64),
        size.width,
        size.height,
    ))
}

/// Creates the 3 slot pairs and transitions every image to `GENERAL`
/// once (all later copies keep that layout).
fn create_generation(
    device: &Arc<HeadlessDevice>,
    mode: ExportMode,
    pool: vk::CommandPool,
    buffer: vk::CommandBuffer,
    fence: vk::Fence,
    width: u32,
    height: u32,
) -> Result<[SlotPair; SLOTS]> {
    let make_pair = || -> Result<SlotPair> {
        Ok(SlotPair {
            y: ExportedImage::new(device, mode, vk::Format::R8_UNORM, width, height)?,
            uv: ExportedImage::new(
                device,
                mode,
                vk::Format::R8G8_UNORM,
                width.checked_div(2).unwrap_or(0),
                height.checked_div(2).unwrap_or(0),
            )?,
        })
    };
    let slots = [make_pair()?, make_pair()?, make_pair()?];

    let vk_device = device.device();
    device.submit_and_wait(pool, buffer, fence, |cmd| {
        let barriers: Vec<vk::ImageMemoryBarrier> = slots
            .iter()
            .flat_map(|slot| [slot.y.image, slot.uv.image])
            .map(|image| {
                vk::ImageMemoryBarrier::default()
                    .src_access_mask(vk::AccessFlags::empty())
                    .dst_access_mask(vk::AccessFlags::TRANSFER_WRITE)
                    .old_layout(vk::ImageLayout::UNDEFINED)
                    .new_layout(vk::ImageLayout::GENERAL)
                    .src_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                    .dst_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                    .image(image)
                    .subresource_range(color_range())
            })
            .collect();
        unsafe {
            vk_device.cmd_pipeline_barrier(
                cmd,
                vk::PipelineStageFlags::TOP_OF_PIPE,
                vk::PipelineStageFlags::TRANSFER,
                vk::DependencyFlags::empty(),
                &[],
                &[],
                &barriers,
            );
        }
        Ok(())
    })?;
    Ok(slots)
}

/// Ships every plane fd over the surface channel (slot-major,
/// plane-minor), then the in-band `TextureSet` describing the layout.
#[allow(clippy::too_many_arguments)]
fn announce(
    surface: &mut fd_channel::Sender,
    channel: &mut Channel,
    id: PlayerId,
    generation: u32,
    mode: ExportMode,
    width: u32,
    height: u32,
    slots: &mut [SlotPair; SLOTS],
) -> Result<()> {
    let mut planes = Vec::with_capacity(SLOTS * 2);
    for (slot_index, slot) in slots.iter_mut().enumerate() {
        for (plane, image) in [(0u8, &mut slot.y), (1u8, &mut slot.uv)] {
            let fd = image
                .fd
                .take()
                .ok_or_else(|| anyhow!("slot image was already announced"))?;
            surface.send(
                fd_channel::SurfaceTag {
                    player: id,
                    generation,
                    #[allow(clippy::cast_possible_truncation)] // SLOTS = 3
                    slot: slot_index as u8,
                    plane,
                },
                fd.as_raw_fd(),
            )?;
            // ours closes here; the client owns the kernel's duplicate
            drop(fd);
            #[allow(clippy::cast_possible_truncation)] // SLOTS = 3
            planes.push(TexturePlaneWire {
                slot: slot_index as u8,
                plane,
                offset: image.offset,
                pitch: image.pitch,
                size: image.size,
                modifier: image.modifier,
            });
        }
    }
    channel.send(&ToClient::TextureSet {
        id,
        generation,
        width,
        height,
        // surfaces travel as tagged fds, not handles
        handles: Vec::new(),
        import: match mode {
            ExportMode::DmaBuf => TextureImportWire::DmaBuf,
            ExportMode::OpaqueFd => TextureImportWire::OpaqueFd {
                tiling_optimal: true,
            },
        },
        planes,
    })
}

/// Copies the core's presentation planes into one shared slot,
/// synchronously (fenced), so the publish that follows only ever
/// announces completed GPU work.
#[allow(clippy::too_many_arguments)]
fn copy_planes(
    device: &Arc<HeadlessDevice>,
    pool: vk::CommandPool,
    buffer: vk::CommandBuffer,
    fence: vk::Fence,
    core_y: vk::Image,
    core_uv: vk::Image,
    slot: &SlotPair,
    width: u32,
    height: u32,
) -> Result<()> {
    let vk_device = device.device();
    let family = device.queue_family();
    // the peer is another Vulkan instance (Unity's), not a non-Vulkan
    // producer — EXTERNAL, unlike the decode import's FOREIGN
    let foreign = vk::QUEUE_FAMILY_EXTERNAL;

    device.submit_and_wait(pool, buffer, fence, |cmd| {
        // reacquire the slot images from the (possibly reading) client
        // and order this copy after the core's own fenced present
        let acquires: Vec<vk::ImageMemoryBarrier> = [slot.y.image, slot.uv.image]
            .into_iter()
            .map(|image| {
                vk::ImageMemoryBarrier::default()
                    .src_access_mask(vk::AccessFlags::empty())
                    .dst_access_mask(vk::AccessFlags::TRANSFER_WRITE)
                    .old_layout(vk::ImageLayout::GENERAL)
                    .new_layout(vk::ImageLayout::GENERAL)
                    .src_queue_family_index(foreign)
                    .dst_queue_family_index(family)
                    .image(image)
                    .subresource_range(color_range())
            })
            .chain([core_y, core_uv].into_iter().map(|image| {
                vk::ImageMemoryBarrier::default()
                    .src_access_mask(vk::AccessFlags::TRANSFER_WRITE)
                    .dst_access_mask(vk::AccessFlags::TRANSFER_READ)
                    .old_layout(vk::ImageLayout::GENERAL)
                    .new_layout(vk::ImageLayout::GENERAL)
                    .src_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                    .dst_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                    .image(image)
                    .subresource_range(color_range())
            }))
            .collect();
        unsafe {
            vk_device.cmd_pipeline_barrier(
                cmd,
                vk::PipelineStageFlags::TRANSFER,
                vk::PipelineStageFlags::TRANSFER,
                vk::DependencyFlags::empty(),
                &[],
                &[],
                &acquires,
            );
        }

        let copy = |extent_w: u32, extent_h: u32| {
            vk::ImageCopy::default()
                .src_subresource(subresource_layers())
                .dst_subresource(subresource_layers())
                .extent(vk::Extent3D {
                    width: extent_w,
                    height: extent_h,
                    depth: 1,
                })
        };
        unsafe {
            vk_device.cmd_copy_image(
                cmd,
                core_y,
                vk::ImageLayout::GENERAL,
                slot.y.image,
                vk::ImageLayout::GENERAL,
                &[copy(width, height)],
            );
            vk_device.cmd_copy_image(
                cmd,
                core_uv,
                vk::ImageLayout::GENERAL,
                slot.uv.image,
                vk::ImageLayout::GENERAL,
                &[copy(
                    width.checked_div(2).unwrap_or(0),
                    height.checked_div(2).unwrap_or(0),
                )],
            );
        }

        // release the written slots back toward the foreign reader
        let releases: Vec<vk::ImageMemoryBarrier> = [slot.y.image, slot.uv.image]
            .into_iter()
            .map(|image| {
                vk::ImageMemoryBarrier::default()
                    .src_access_mask(vk::AccessFlags::TRANSFER_WRITE)
                    .dst_access_mask(vk::AccessFlags::empty())
                    .old_layout(vk::ImageLayout::GENERAL)
                    .new_layout(vk::ImageLayout::GENERAL)
                    .src_queue_family_index(family)
                    .dst_queue_family_index(foreign)
                    .image(image)
                    .subresource_range(color_range())
            })
            .collect();
        unsafe {
            vk_device.cmd_pipeline_barrier(
                cmd,
                vk::PipelineStageFlags::TRANSFER,
                vk::PipelineStageFlags::BOTTOM_OF_PIPE,
                vk::DependencyFlags::empty(),
                &[],
                &[],
                &releases,
            );
        }
        Ok(())
    })
}

impl ExportedImage {
    fn new(
        device: &Arc<HeadlessDevice>,
        mode: ExportMode,
        format: vk::Format,
        width: u32,
        height: u32,
    ) -> Result<Self> {
        ensure!(width > 0 && height > 0, "slot plane has no size");
        let vk_device = device.device();

        let handle_type = match mode {
            ExportMode::DmaBuf => vk::ExternalMemoryHandleTypeFlags::DMA_BUF_EXT,
            ExportMode::OpaqueFd => vk::ExternalMemoryHandleTypeFlags::OPAQUE_FD,
        };
        let mut external_info =
            vk::ExternalMemoryImageCreateInfo::default().handle_types(handle_type);
        let modifiers = [MOD_LINEAR];
        let mut modifier_info =
            vk::ImageDrmFormatModifierListCreateInfoEXT::default().drm_format_modifiers(&modifiers);
        let tiling = match mode {
            ExportMode::DmaBuf => vk::ImageTiling::DRM_FORMAT_MODIFIER_EXT,
            ExportMode::OpaqueFd => vk::ImageTiling::OPTIMAL,
        };
        let mut info = vk::ImageCreateInfo::default()
            .image_type(vk::ImageType::TYPE_2D)
            .format(format)
            .extent(vk::Extent3D {
                width,
                height,
                depth: 1,
            })
            .mip_levels(1)
            .array_layers(1)
            .samples(vk::SampleCountFlags::TYPE_1)
            .tiling(tiling)
            .usage(
                vk::ImageUsageFlags::SAMPLED
                    | vk::ImageUsageFlags::TRANSFER_SRC
                    | vk::ImageUsageFlags::TRANSFER_DST,
            )
            .sharing_mode(vk::SharingMode::EXCLUSIVE)
            .initial_layout(vk::ImageLayout::UNDEFINED)
            .push_next(&mut external_info);
        if mode == ExportMode::DmaBuf {
            info = info.push_next(&mut modifier_info);
        }
        let image = unsafe { vk_device.create_image(&info, None) }
            .with_context(|| format!("create exportable {format:?} {width}x{height} image"))?;
        // guard everything below; ownership moves into Self at the end
        let mut guard = ImageGuard {
            device: Arc::clone(device),
            image,
            memory: vk::DeviceMemory::null(),
        };

        let reqs = unsafe { vk_device.get_image_memory_requirements(image) };
        let type_index = device
            .memory_type_index(reqs.memory_type_bits, vk::MemoryPropertyFlags::DEVICE_LOCAL)?;
        let mut export_info = vk::ExportMemoryAllocateInfo::default().handle_types(handle_type);
        let mut dedicated = vk::MemoryDedicatedAllocateInfo::default().image(image);
        let alloc = vk::MemoryAllocateInfo::default()
            .allocation_size(reqs.size)
            .memory_type_index(type_index)
            .push_next(&mut export_info)
            .push_next(&mut dedicated);
        let memory = unsafe { vk_device.allocate_memory(&alloc, None) }
            .context("allocate exportable memory")?;
        guard.memory = memory;
        unsafe { vk_device.bind_image_memory(image, memory, 0) }.context("bind slot memory")?;

        let get_info = vk::MemoryGetFdInfoKHR::default()
            .memory(memory)
            .handle_type(handle_type);
        let raw_fd = unsafe { device.external_memory_fd().get_memory_fd(&get_info) }
            .context("export slot memory fd")?;
        let fd = unsafe { OwnedFd::from_raw_fd(raw_fd) };

        let (offset, pitch, modifier) = match mode {
            ExportMode::DmaBuf => {
                let subresource = vk::ImageSubresource {
                    aspect_mask: vk::ImageAspectFlags::MEMORY_PLANE_0_EXT,
                    mip_level: 0,
                    array_layer: 0,
                };
                let layout =
                    unsafe { vk_device.get_image_subresource_layout(image, subresource) };
                (
                    layout.offset,
                    u32::try_from(layout.row_pitch).context("slot row pitch")?,
                    MOD_LINEAR,
                )
            }
            ExportMode::OpaqueFd => (0, 0, 0),
        };

        // disarm: ownership of both handles moves into Self
        guard.image = vk::Image::null();
        guard.memory = vk::DeviceMemory::null();
        drop(guard);
        Ok(Self {
            device: Arc::clone(device),
            image,
            memory,
            offset,
            pitch,
            size: reqs.size,
            modifier,
            fd: Some(fd),
        })
    }
}

/// Cleans up a partially constructed slot image on error paths.
struct ImageGuard {
    device: Arc<HeadlessDevice>,
    image: vk::Image,
    memory: vk::DeviceMemory,
}

impl Drop for ImageGuard {
    fn drop(&mut self) {
        unsafe {
            let device = self.device.device();
            if self.image != vk::Image::null() {
                device.destroy_image(self.image, None);
            }
            if self.memory != vk::DeviceMemory::null() {
                device.free_memory(self.memory, None);
            }
        }
    }
}

const fn color_range() -> vk::ImageSubresourceRange {
    vk::ImageSubresourceRange {
        aspect_mask: vk::ImageAspectFlags::COLOR,
        base_mip_level: 0,
        level_count: 1,
        base_array_layer: 0,
        layer_count: 1,
    }
}

const fn subresource_layers() -> vk::ImageSubresourceLayers {
    vk::ImageSubresourceLayers {
        aspect_mask: vk::ImageAspectFlags::COLOR,
        mip_level: 0,
        base_array_layer: 0,
        layer_count: 1,
    }
}
