//! Vulkan backend of the Linux presentation: imports the helper's
//! dma-buf slot planes as images on Unity's device and copies the
//! published slot into owned presentation images (Y `R8`, UV `R8G8`).
//!
//! Copies are recorded into Unity's current command buffer
//! ([`VulkanDevice::with_command_buffer`]), so they execute before the
//! C# blit that samples the presentation planes later in the same
//! frame. The presentation images live in `SHADER_READ_ONLY_OPTIMAL`
//! between copies — the layout Unity expects of an external texture.

use crate::platform::{UnityDevice, VulkanDevice};
use std::sync::Arc;
use crate::present::{PLANES, SLOTS};
use anyhow::{Context as _, Result, anyhow, ensure};
use ash::vk;
use ash::vk::Handle as _;
use std::os::fd::{IntoRawFd as _, OwnedFd};
use std::os::raw::c_void;
use uuav_ipc::protocol::{TextureImportWire, TexturePlaneWire};

/// One imported slot plane; freed with its generation.
struct ImportedPlane {
    image: vk::Image,
    memory: vk::DeviceMemory,
}

/// The 6 imported plane images of one generation, slot-major.
pub struct ImportedSlots {
    /// Keeps the (possibly owned/headless) device alive past every
    /// holder of these images — the drop below calls into it.
    _keepalive: Arc<UnityDevice>,
    device: ash::Device,
    planes: Vec<ImportedPlane>,
    /// Set once the first copy acquired the images from EXTERNAL.
    acquired: std::cell::Cell<bool>,
}

// only touched behind the mirror's mutex
unsafe impl Send for ImportedSlots {}

impl Drop for ImportedSlots {
    fn drop(&mut self) {
        unsafe {
            for plane in &self.planes {
                self.device.destroy_image(plane.image, None);
                self.device.free_memory(plane.memory, None);
            }
        }
    }
}

/// The owned presentation planes C# wraps.
pub struct PresentationPlanes {
    _keepalive: Arc<UnityDevice>,
    device: ash::Device,
    y: ImportedPlane,
    uv: ImportedPlane,
    pub width: u32,
    pub height: u32,
    initialized: bool,
}

unsafe impl Send for PresentationPlanes {}

impl Drop for PresentationPlanes {
    fn drop(&mut self) {
        unsafe {
            for plane in [&self.y, &self.uv] {
                self.device.destroy_image(plane.image, None);
                self.device.free_memory(plane.memory, None);
            }
        }
    }
}

impl PresentationPlanes {
    pub fn new(
        keepalive: Arc<UnityDevice>,
        unity: &VulkanDevice,
        width: u32,
        height: u32,
    ) -> Result<Self> {
        let y = owned_plane(unity, vk::Format::R8_UNORM, width, height)?;
        let uv = match owned_plane(
            unity,
            vk::Format::R8G8_UNORM,
            width.checked_div(2).unwrap_or(0),
            height.checked_div(2).unwrap_or(0),
        ) {
            Ok(uv) => uv,
            Err(e) => {
                unsafe {
                    unity.device.destroy_image(y.image, None);
                    unity.device.free_memory(y.memory, None);
                }
                return Err(e);
            }
        };
        Ok(Self {
            _keepalive: keepalive,
            device: unity.device.clone(),
            y,
            uv,
            width,
            height,
            initialized: false,
        })
    }

    /// The `VkImage` handle C# hands to `CreateExternalTexture`.
    pub fn texture_ptr(&self, plane: i32) -> *const c_void {
        let image = if plane == 0 { self.y.image } else { self.uv.image };
        image.as_raw() as usize as *const c_void
    }
}

/// Imports the 6 dma-buf fds of a generation as plane images.
pub fn import_generation(
    keepalive: Arc<UnityDevice>,
    unity: &VulkanDevice,
    import: TextureImportWire,
    layout: &[TexturePlaneWire],
    fds: [[Option<OwnedFd>; PLANES]; SLOTS],
    width: u32,
    height: u32,
) -> Result<ImportedSlots> {
    ensure!(
        import == TextureImportWire::DmaBuf,
        "a Vulkan client expects dma-buf texture sets"
    );
    let mut slots = ImportedSlots {
        _keepalive: keepalive,
        device: unity.device.clone(),
        planes: Vec::with_capacity(SLOTS * PLANES),
        acquired: std::cell::Cell::new(false),
    };
    for (slot_index, slot) in fds.into_iter().enumerate() {
        for (plane_index, fd) in slot.into_iter().enumerate() {
            let fd = fd.ok_or_else(|| anyhow!("missing fd for slot {slot_index}"))?;
            let wire = layout
                .iter()
                .find(|p| {
                    usize::from(p.slot) == slot_index && usize::from(p.plane) == plane_index
                })
                .ok_or_else(|| anyhow!("missing layout for slot {slot_index}"))?;
            let (format, plane_width, plane_height) = if plane_index == 0 {
                (vk::Format::R8_UNORM, width, height)
            } else {
                (
                    vk::Format::R8G8_UNORM,
                    width.checked_div(2).unwrap_or(0),
                    height.checked_div(2).unwrap_or(0),
                )
            };
            let plane = import_plane(unity, wire, fd, format, plane_width, plane_height)
                .with_context(|| format!("import slot {slot_index} plane {plane_index}"))?;
            slots.planes.push(plane);
        }
    }
    Ok(slots)
}

/// One dma-buf plane image on Unity's device, with the exporter's
/// explicit modifier layout.
fn import_plane(
    unity: &VulkanDevice,
    wire: &TexturePlaneWire,
    fd: OwnedFd,
    format: vk::Format,
    width: u32,
    height: u32,
) -> Result<ImportedPlane> {
    ensure!(width > 0 && height > 0, "plane has no size");
    let device = &unity.device;

    let layouts = [vk::SubresourceLayout {
        offset: wire.offset,
        size: 0,
        row_pitch: u64::from(wire.pitch),
        array_pitch: 0,
        depth_pitch: 0,
    }];
    let mut modifier_info = vk::ImageDrmFormatModifierExplicitCreateInfoEXT::default()
        .drm_format_modifier(wire.modifier)
        .plane_layouts(&layouts);
    let mut external_info = vk::ExternalMemoryImageCreateInfo::default()
        .handle_types(vk::ExternalMemoryHandleTypeFlags::DMA_BUF_EXT);
    let info = vk::ImageCreateInfo::default()
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
        .tiling(vk::ImageTiling::DRM_FORMAT_MODIFIER_EXT)
        .usage(vk::ImageUsageFlags::TRANSFER_SRC)
        .sharing_mode(vk::SharingMode::EXCLUSIVE)
        .initial_layout(vk::ImageLayout::UNDEFINED)
        .push_next(&mut modifier_info)
        .push_next(&mut external_info);
    let image = unsafe { device.create_image(&info, None) }
        .with_context(|| format!("import {format:?} image (modifier {:#x})", wire.modifier))?;

    let destroy_image = || unsafe { device.destroy_image(image, None) };

    // ownership of the fd transfers into Vulkan on success only
    let raw_fd = fd.into_raw_fd();
    let close_fd = || unsafe {
        libc::close(raw_fd);
    };

    let mut fd_props = vk::MemoryFdPropertiesKHR::default();
    if let Err(e) = unsafe {
        unity.external_memory_fd.get_memory_fd_properties(
            vk::ExternalMemoryHandleTypeFlags::DMA_BUF_EXT,
            raw_fd,
            &mut fd_props,
        )
    } {
        close_fd();
        destroy_image();
        return Err(e).context("query dma-buf memory properties");
    }
    let reqs = unsafe { device.get_image_memory_requirements(image) };
    let type_bits = reqs.memory_type_bits & fd_props.memory_type_bits;
    let type_index =
        match unity.memory_type_index(type_bits, vk::MemoryPropertyFlags::DEVICE_LOCAL) {
            Ok(index) => index,
            Err(e) => {
                close_fd();
                destroy_image();
                return Err(e);
            }
        };

    let mut import_info = vk::ImportMemoryFdInfoKHR::default()
        .handle_type(vk::ExternalMemoryHandleTypeFlags::DMA_BUF_EXT)
        .fd(raw_fd);
    let mut dedicated = vk::MemoryDedicatedAllocateInfo::default().image(image);
    let alloc = vk::MemoryAllocateInfo::default()
        .allocation_size(wire.size)
        .memory_type_index(type_index)
        .push_next(&mut import_info)
        .push_next(&mut dedicated);
    let memory = match unsafe { device.allocate_memory(&alloc, None) } {
        Ok(memory) => memory,
        Err(e) => {
            close_fd();
            destroy_image();
            return Err(e).context("import dma-buf memory");
        }
    };
    if let Err(e) = unsafe { device.bind_image_memory(image, memory, 0) } {
        unsafe { device.free_memory(memory, None) };
        destroy_image();
        return Err(e).context("bind imported memory");
    }
    Ok(ImportedPlane { image, memory })
}

/// Copies one published slot into the presentation planes, recorded in
/// Unity's command stream.
pub fn copy_slot(
    unity: &VulkanDevice,
    slots: &ImportedSlots,
    slot: u8,
    planes: &mut PresentationPlanes,
) -> Result<()> {
    let base = usize::from(slot).saturating_mul(PLANES);
    let source_y = slots.planes.get(base).context("published slot out of range")?;
    let source_uv = slots
        .planes
        .get(base.saturating_add(1))
        .context("published slot out of range")?;

    let family = unity.queue_family;
    let first_acquire = !slots.acquired.get();
    let first_use = !planes.initialized;

    unity.with_command_buffer(|device, cmd| {
        let barriers = pre_copy_barriers(
            [source_y.image, source_uv.image],
            [planes.y.image, planes.uv.image],
            family,
            first_acquire,
            first_use,
        );
        unsafe {
            device.cmd_pipeline_barrier(
                cmd,
                vk::PipelineStageFlags::FRAGMENT_SHADER | vk::PipelineStageFlags::TOP_OF_PIPE,
                vk::PipelineStageFlags::TRANSFER,
                vk::DependencyFlags::empty(),
                &[],
                &[],
                &barriers,
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
            device.cmd_copy_image(
                cmd,
                source_y.image,
                vk::ImageLayout::GENERAL,
                planes.y.image,
                vk::ImageLayout::TRANSFER_DST_OPTIMAL,
                &[copy(planes.width, planes.height)],
            );
            device.cmd_copy_image(
                cmd,
                source_uv.image,
                vk::ImageLayout::GENERAL,
                planes.uv.image,
                vk::ImageLayout::TRANSFER_DST_OPTIMAL,
                &[copy(
                    planes.width.checked_div(2).unwrap_or(0),
                    planes.height.checked_div(2).unwrap_or(0),
                )],
            );
        }

        // back to the layout Unity samples external textures in
        let to_sampled: Vec<vk::ImageMemoryBarrier> = [&planes.y, &planes.uv]
            .into_iter()
            .map(|plane| {
                vk::ImageMemoryBarrier::default()
                    .src_access_mask(vk::AccessFlags::TRANSFER_WRITE)
                    .dst_access_mask(vk::AccessFlags::SHADER_READ)
                    .old_layout(vk::ImageLayout::TRANSFER_DST_OPTIMAL)
                    .new_layout(vk::ImageLayout::SHADER_READ_ONLY_OPTIMAL)
                    .src_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                    .dst_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                    .image(plane.image)
                    .subresource_range(color_range())
            })
            .collect();
        unsafe {
            device.cmd_pipeline_barrier(
                cmd,
                vk::PipelineStageFlags::TRANSFER,
                vk::PipelineStageFlags::FRAGMENT_SHADER,
                vk::DependencyFlags::empty(),
                &[],
                &[],
                &to_sampled,
            );
        }
        Ok(())
    })?;

    slots.acquired.set(true);
    planes.initialized = true;
    Ok(())
}

/// Barriers before the slot copy: acquire the shared source images from
/// the helper's release (first copy of a generation only; after that
/// they stay ours and the helper re-acquires per write), and move the
/// presentation planes into transfer-dst.
fn pre_copy_barriers(
    sources: [vk::Image; 2],
    dests: [vk::Image; 2],
    family: u32,
    first_acquire: bool,
    first_use: bool,
) -> Vec<vk::ImageMemoryBarrier<'static>> {
    let mut barriers = Vec::with_capacity(4);
    for image in sources {
        barriers.push(
            vk::ImageMemoryBarrier::default()
                .src_access_mask(vk::AccessFlags::empty())
                .dst_access_mask(vk::AccessFlags::TRANSFER_READ)
                .old_layout(vk::ImageLayout::GENERAL)
                .new_layout(vk::ImageLayout::GENERAL)
                .src_queue_family_index(if first_acquire {
                    vk::QUEUE_FAMILY_EXTERNAL
                } else {
                    vk::QUEUE_FAMILY_IGNORED
                })
                .dst_queue_family_index(if first_acquire {
                    family
                } else {
                    vk::QUEUE_FAMILY_IGNORED
                })
                .image(image)
                .subresource_range(color_range()),
        );
    }
    for image in dests {
        barriers.push(
            vk::ImageMemoryBarrier::default()
                .src_access_mask(vk::AccessFlags::empty())
                .dst_access_mask(vk::AccessFlags::TRANSFER_WRITE)
                .old_layout(if first_use {
                    vk::ImageLayout::UNDEFINED
                } else {
                    vk::ImageLayout::SHADER_READ_ONLY_OPTIMAL
                })
                .new_layout(vk::ImageLayout::TRANSFER_DST_OPTIMAL)
                .src_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                .dst_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                .image(image)
                .subresource_range(color_range()),
        );
    }
    barriers
}

/// One owned presentation plane on Unity's device.
fn owned_plane(
    unity: &VulkanDevice,
    format: vk::Format,
    width: u32,
    height: u32,
) -> Result<ImportedPlane> {
    ensure!(width > 0 && height > 0, "plane has no size");
    let device = &unity.device;
    let info = vk::ImageCreateInfo::default()
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
        .tiling(vk::ImageTiling::OPTIMAL)
        .usage(vk::ImageUsageFlags::SAMPLED | vk::ImageUsageFlags::TRANSFER_DST)
        .sharing_mode(vk::SharingMode::EXCLUSIVE)
        .initial_layout(vk::ImageLayout::UNDEFINED);
    let image = unsafe { device.create_image(&info, None) }
        .with_context(|| format!("create {format:?} {width}x{height} presentation image"))?;

    let reqs = unsafe { device.get_image_memory_requirements(image) };
    let allocate = || -> Result<vk::DeviceMemory> {
        let type_index = unity
            .memory_type_index(reqs.memory_type_bits, vk::MemoryPropertyFlags::DEVICE_LOCAL)?;
        let alloc = vk::MemoryAllocateInfo::default()
            .allocation_size(reqs.size)
            .memory_type_index(type_index);
        unsafe { device.allocate_memory(&alloc, None) }.context("allocate presentation memory")
    };
    let memory = match allocate() {
        Ok(memory) => memory,
        Err(e) => {
            unsafe { device.destroy_image(image, None) };
            return Err(e);
        }
    };
    if let Err(e) = unsafe { device.bind_image_memory(image, memory, 0) } {
        unsafe {
            device.free_memory(memory, None);
            device.destroy_image(image, None);
        }
        return Err(e).context("bind presentation memory");
    }
    Ok(ImportedPlane { image, memory })
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
