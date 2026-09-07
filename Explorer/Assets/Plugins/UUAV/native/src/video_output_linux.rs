//! Linux presentation: the Vulkan analog of the Metal path's
//! IOSurface-wrap-and-blit.
//!
//! Each due frame's VAAPI surface is mapped to a DRM PRIME descriptor
//! (`av_hwframe_map`, which syncs the surface and exports dma-buf fds),
//! imported into the shared Vulkan device as an image, and copied into
//! two owned per-plane presentation images (Y `R8`, UV `R8G8`). The
//! copy is fenced synchronously, matching the `waitUntilCompleted`
//! stance: when `present` returns, the mapped frame and its fds are
//! already released.
//!
//! Import handles the two descriptor shapes real drivers produce: one
//! NV12 layer whose planes share or split across objects (imported as a
//! single 2-plane image, disjoint when split), and two single-plane
//! layers (imported as separate R8/GR88 images).

use anyhow::{Context as _, Result, anyhow, ensure};
use ash::vk;
use ash::vk::Handle as _;
use ffmpeg_sys_next as ff;
use std::os::raw::{c_int, c_void};
use std::sync::Arc;

use crate::ffutil::{OwnedFrame, check};
use crate::hw_device::HwDevice;
use crate::linux_device::HeadlessDevice;
use crate::video_decoder::VideoFrame;

const DRM_FORMAT_NV12: u32 = fourcc(*b"NV12");
const DRM_FORMAT_R8: u32 = fourcc(*b"R8  ");
const DRM_FORMAT_GR88: u32 = fourcc(*b"GR88");
const DRM_FORMAT_RG88: u32 = fourcc(*b"RG88");

const fn fourcc(code: [u8; 4]) -> u32 {
    (code[0] as u32) | ((code[1] as u32) << 8) | ((code[2] as u32) << 16) | ((code[3] as u32) << 24)
}

/// Non-owning view of one of [`VideoOutput`]'s presentation planes: the
/// raw `VkImage` handle of plane 0 (Y, `R8`) or 1 (UV, `R8G8`). The
/// handle stays valid until `VideoOutput` retires the generation after
/// a resolution change (contract of `uuav_player_get_video_texture`).
pub(crate) struct VideoTextureView {
    image: vk::Image,
}

impl VideoTextureView {
    pub(crate) fn raw_ptr_mut(&self) -> *mut c_void {
        // VkImage is a non-dispatchable u64 handle; consumers cast it back
        self.image.as_raw() as usize as *mut c_void
    }
}

/// Presentation target: two owned per-plane images on the shared
/// helper device that decoded frames are copied into on the render
/// thread.
pub(crate) struct VideoOutput {
    device: Arc<HeadlessDevice>,
    /// Created lazily on the first present, keeping `new` infallible
    /// like the other platforms'.
    commands: Option<Commands>,
    planes: Option<SizedPlanes>,
    /// Previous plane generation, kept alive for one resolution change
    /// so handles still wrapped by consumers stay valid until their
    /// next poll notices the new plane-0 handle.
    retired: Option<SizedPlanes>,
}

/// The recording/submission plumbing for the fenced presents.
struct Commands {
    pool: vk::CommandPool,
    buffer: vk::CommandBuffer,
    fence: vk::Fence,
}

// used only behind the player's mutex; all Vulkan access is through the
// externally-synchronized wrappers on HeadlessDevice
unsafe impl Send for VideoOutput {}

struct SizedPlanes {
    y: PlaneImage,
    uv: PlaneImage,
    width: u32,
    height: u32,
    /// False until the first copy initialized the images' layout.
    initialized: bool,
}

impl SizedPlanes {
    const fn matches(&self, width: u32, height: u32) -> bool {
        self.width == width && self.height == height
    }
}

impl VideoOutput {
    pub(crate) fn new(device: &HwDevice) -> Self {
        Self {
            device: Arc::clone(device.headless()),
            commands: None,
            planes: None,
            retired: None,
        }
    }

    fn create_commands(device: &HeadlessDevice) -> Result<Commands> {
        let vk_device = device.device();
        unsafe {
            let pool_info = vk::CommandPoolCreateInfo::default()
                .queue_family_index(device.queue_family());
            let pool = vk_device
                .create_command_pool(&pool_info, None)
                .context("create command pool")?;
            let alloc = vk::CommandBufferAllocateInfo::default()
                .command_pool(pool)
                .level(vk::CommandBufferLevel::PRIMARY)
                .command_buffer_count(1);
            let buffer = match alloc_one_buffer(vk_device, &alloc) {
                Ok(buffer) => buffer,
                Err(e) => {
                    vk_device.destroy_command_pool(pool, None);
                    return Err(e);
                }
            };
            let fence = match vk_device.create_fence(&vk::FenceCreateInfo::default(), None) {
                Ok(fence) => fence,
                Err(e) => {
                    vk_device.destroy_command_pool(pool, None);
                    return Err(e).context("create fence");
                }
            };
            Ok(Commands {
                pool,
                buffer,
                fence,
            })
        }
    }

    /// One plane image handle, once the first frame has been presented.
    pub(crate) fn texture(&self, plane: i32) -> Option<VideoTextureView> {
        let planes = self.planes.as_ref()?;
        let image = match plane {
            0 => planes.y.image,
            1 => planes.uv.image,
            _ => return None,
        };
        Some(VideoTextureView { image })
    }

    /// Takes no decode context on purpose (like the Metal sibling): the
    /// mapped dma-buf is an immutable export and the copy runs on the
    /// plugin's own queue.
    pub(crate) fn present(&mut self, frame: &VideoFrame) -> Result<()> {
        // NV12 requires even dimensions; negative decoder sizes fold to 0
        let width = u32::try_from(frame.width()).unwrap_or(0) & !1;
        let height = u32::try_from(frame.height()).unwrap_or(0) & !1;
        if width == 0 || height == 0 {
            return Err(anyhow!("frame has no presentable size"));
        }
        self.ensure_planes(width, height)?;
        if self.commands.is_none() {
            self.commands = Some(Self::create_commands(&self.device)?);
        }

        let mapped = MappedDrmFrame::new(frame)?;
        let source = ImportedSource::import(&self.device, mapped.descriptor()?, width, height)?;

        let commands = self
            .commands
            .as_ref()
            .ok_or_else(|| anyhow!("command plumbing is missing"))?;
        let planes = self
            .planes
            .as_mut()
            .ok_or_else(|| anyhow!("presentation planes are missing"))?;
        let first_use = !planes.initialized;

        let device = Arc::clone(&self.device);
        let vk_device = device.device();
        let external_src = device.external_src_family();
        let family = device.queue_family();

        device.submit_and_wait(commands.pool, commands.buffer, commands.fence, |cmd| {
            // acquire the imported image(s) from the foreign producer;
            // GENERAL->GENERAL preserves the decoded contents
            let mut barriers: Vec<vk::ImageMemoryBarrier> = source
                .images
                .iter()
                .map(|&image| {
                    vk::ImageMemoryBarrier::default()
                        .src_access_mask(vk::AccessFlags::empty())
                        .dst_access_mask(vk::AccessFlags::TRANSFER_READ)
                        .old_layout(vk::ImageLayout::GENERAL)
                        .new_layout(vk::ImageLayout::GENERAL)
                        .src_queue_family_index(external_src)
                        .dst_queue_family_index(family)
                        .image(image)
                        // COLOR covers every plane of a multi-planar
                        // image in barriers
                        .subresource_range(color_range())
                })
                .collect();
            for plane in [&planes.y, &planes.uv] {
                barriers.push(
                    vk::ImageMemoryBarrier::default()
                        .src_access_mask(vk::AccessFlags::empty())
                        .dst_access_mask(vk::AccessFlags::TRANSFER_WRITE)
                        .old_layout(if first_use {
                            vk::ImageLayout::UNDEFINED
                        } else {
                            vk::ImageLayout::GENERAL
                        })
                        .new_layout(vk::ImageLayout::GENERAL)
                        .src_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                        .dst_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                        .image(plane.image)
                        .subresource_range(color_range()),
                );
            }
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

            source.record_copies(vk_device, cmd, planes, width, height);

            // make the writes visible to the pump's follow-up copies on
            // this same queue
            let flushes: Vec<vk::ImageMemoryBarrier> = [&planes.y, &planes.uv]
                .into_iter()
                .map(|plane| {
                    vk::ImageMemoryBarrier::default()
                        .src_access_mask(vk::AccessFlags::TRANSFER_WRITE)
                        .dst_access_mask(vk::AccessFlags::TRANSFER_READ | vk::AccessFlags::SHADER_READ)
                        .old_layout(vk::ImageLayout::GENERAL)
                        .new_layout(vk::ImageLayout::GENERAL)
                        .src_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                        .dst_queue_family_index(vk::QUEUE_FAMILY_IGNORED)
                        .image(plane.image)
                        .subresource_range(color_range())
                })
                .collect();
            unsafe {
                vk_device.cmd_pipeline_barrier(
                    cmd,
                    vk::PipelineStageFlags::TRANSFER,
                    vk::PipelineStageFlags::TRANSFER | vk::PipelineStageFlags::FRAGMENT_SHADER,
                    vk::DependencyFlags::empty(),
                    &[],
                    &[],
                    &flushes,
                );
            }
            Ok(())
        })?;

        planes.initialized = true;
        // source + mapped drop here: the fence proved the GPU is done
        Ok(())
    }

    /// Ensures the owned plane images match the size, recreating them on
    /// change and retiring the previous generation for one more poll
    /// cycle.
    fn ensure_planes(&mut self, width: u32, height: u32) -> Result<()> {
        if self
            .planes
            .as_ref()
            .is_some_and(|planes| planes.matches(width, height))
        {
            return Ok(());
        }

        let y = PlaneImage::new(&self.device, vk::Format::R8_UNORM, width, height)?;
        let uv = PlaneImage::new(
            &self.device,
            vk::Format::R8G8_UNORM,
            width.checked_div(2).unwrap_or(0),
            height.checked_div(2).unwrap_or(0),
        )?;
        // the generation before last dies here; consumers have had a
        // full poll cycle to stop wrapping it
        let _previous = self.retired.take();
        self.retired = self.planes.take();
        self.planes = Some(SizedPlanes {
            y,
            uv,
            width,
            height,
            initialized: false,
        });
        Ok(())
    }
}

impl Drop for VideoOutput {
    fn drop(&mut self) {
        // planes/retired hold their own device references and clean up
        // after this
        if let Some(commands) = self.commands.take() {
            unsafe {
                let vk_device = self.device.device();
                vk_device.destroy_fence(commands.fence, None);
                vk_device.destroy_command_pool(commands.pool, None);
            }
        }
    }
}

/// Allocates exactly one primary command buffer.
fn alloc_one_buffer(
    vk_device: &ash::Device,
    alloc: &vk::CommandBufferAllocateInfo,
) -> Result<vk::CommandBuffer> {
    let buffers =
        unsafe { vk_device.allocate_command_buffers(alloc) }.context("allocate command buffer")?;
    buffers.first().copied().context("no command buffer")
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

/// One owned destination plane the pump copies from (and, in-process,
/// the engine samples).
struct PlaneImage {
    device: Arc<HeadlessDevice>,
    image: vk::Image,
    memory: vk::DeviceMemory,
}

impl PlaneImage {
    fn new(device: &Arc<HeadlessDevice>, format: vk::Format, width: u32, height: u32) -> Result<Self> {
        ensure!(width > 0 && height > 0, "plane has no size");
        let vk_device = device.device();
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
            .usage(
                vk::ImageUsageFlags::SAMPLED
                    | vk::ImageUsageFlags::TRANSFER_SRC
                    | vk::ImageUsageFlags::TRANSFER_DST,
            )
            .sharing_mode(vk::SharingMode::EXCLUSIVE)
            .initial_layout(vk::ImageLayout::UNDEFINED);
        let image = unsafe { vk_device.create_image(&info, None) }
            .with_context(|| format!("create {format:?} {width}x{height} plane image"))?;

        let reqs = unsafe { vk_device.get_image_memory_requirements(image) };
        let allocate = || -> Result<vk::DeviceMemory> {
            let type_index = device
                .memory_type_index(reqs.memory_type_bits, vk::MemoryPropertyFlags::DEVICE_LOCAL)?;
            let alloc = vk::MemoryAllocateInfo::default()
                .allocation_size(reqs.size)
                .memory_type_index(type_index);
            unsafe { vk_device.allocate_memory(&alloc, None) }.context("allocate plane memory")
        };
        let memory = match allocate() {
            Ok(memory) => memory,
            Err(e) => {
                unsafe { vk_device.destroy_image(image, None) };
                return Err(e);
            }
        };
        if let Err(e) = unsafe { vk_device.bind_image_memory(image, memory, 0) } {
            unsafe {
                vk_device.free_memory(memory, None);
                vk_device.destroy_image(image, None);
            }
            return Err(e).context("bind plane memory");
        }
        Ok(Self {
            device: Arc::clone(device),
            image,
            memory,
        })
    }
}

impl Drop for PlaneImage {
    fn drop(&mut self) {
        unsafe {
            let vk_device = self.device.device();
            vk_device.destroy_image(self.image, None);
            vk_device.free_memory(self.memory, None);
        }
    }
}

/// The decoded frame mapped to DRM PRIME. `av_hwframe_map` syncs the
/// VAAPI surface first; dropping this unrefs the mapped frame, which
/// closes the exported fds (Vulkan imports work on duplicates).
struct MappedDrmFrame {
    frame: OwnedFrame,
}

impl MappedDrmFrame {
    fn new(source: &VideoFrame) -> Result<Self> {
        let mut frame = OwnedFrame::new()?;
        unsafe {
            (*frame.as_mut_ptr()).format = ff::AVPixelFormat::AV_PIX_FMT_DRM_PRIME as c_int;
        }
        #[allow(clippy::cast_possible_wrap, clippy::cast_possible_truncation)]
        let flags = ff::AV_HWFRAME_MAP_READ as c_int;
        check("av_hwframe_map(vaapi->drm)", unsafe {
            ff::av_hwframe_map(frame.as_mut_ptr(), source.av_frame(), flags)
        })?;
        Ok(Self { frame })
    }

    fn descriptor(&self) -> Result<&ff::AVDRMFrameDescriptor> {
        let ptr = self.frame.data(0).cast::<ff::AVDRMFrameDescriptor>();
        ensure!(!ptr.is_null(), "mapped frame carries no DRM descriptor");
        Ok(unsafe { &*ptr })
    }
}

/// How the descriptor's layers map onto Vulkan images.
enum ImportShape {
    /// One NV12 layer: a single 2-plane image; copies select planes by
    /// aspect.
    TwoPlaneImage,
    /// Two single-plane layers: separate R8 and GR88 images.
    PerPlaneImages,
}

/// The imported decoded frame: image(s) bound to dma-buf memory. Alive
/// only for the duration of one fenced present.
struct ImportedSource {
    device: Arc<HeadlessDevice>,
    images: Vec<vk::Image>,
    memories: Vec<vk::DeviceMemory>,
    shape: ImportShape,
}

impl ImportedSource {
    fn import(
        device: &Arc<HeadlessDevice>,
        desc: &ff::AVDRMFrameDescriptor,
        width: u32,
        height: u32,
    ) -> Result<Self> {
        let objects = usize::try_from(desc.nb_objects).context("descriptor objects")?;
        let layers = usize::try_from(desc.nb_layers).context("descriptor layers")?;
        ensure!(
            (1..=2).contains(&objects) && (1..=2).contains(&layers),
            "unsupported DRM frame shape: {objects} objects, {layers} layers"
        );
        let object_slice = desc.objects.get(..objects).context("object bounds")?;
        let layer_slice = desc.layers.get(..layers).context("layer bounds")?;

        let mut imported = Self {
            device: Arc::clone(device),
            images: Vec::new(),
            memories: Vec::new(),
            shape: ImportShape::PerPlaneImages,
        };
        if layers == 1 {
            imported.import_nv12_layer(object_slice, layer_slice, width, height)?;
        } else {
            imported.import_plane_layers(object_slice, layer_slice, width, height)?;
        }
        Ok(imported)
    }

    /// One NV12 layer whose two planes share or split across objects: a
    /// single 2-plane image (disjoint when split).
    fn import_nv12_layer(
        &mut self,
        object_slice: &[ff::AVDRMObjectDescriptor],
        layer_slice: &[ff::AVDRMLayerDescriptor],
        width: u32,
        height: u32,
    ) -> Result<()> {
        let layer = layer_slice.first().context("layer bounds")?;
        ensure!(
            layer.format == DRM_FORMAT_NV12,
            "single-layer format {:#x} is not NV12",
            layer.format
        );
        let planes = usize::try_from(layer.nb_planes).context("layer planes")?;
        ensure!(planes == 2, "NV12 layer has {planes} planes");
        let plane_slice = layer.planes.get(..planes).context("plane bounds")?;
        let first_object =
            usize::try_from(plane_slice.first().context("plane bounds")?.object_index)
                .context("plane object")?;
        let disjoint = plane_slice.iter().any(|plane| {
            usize::try_from(plane.object_index).is_ok_and(|index| index != first_object)
        });

        let modifier = object_slice
            .first()
            .context("object bounds")?
            .format_modifier;
        ensure!(
            object_slice.iter().all(|o| o.format_modifier == modifier),
            "objects disagree on the format modifier"
        );

        let layouts: Vec<vk::SubresourceLayout> =
            plane_slice.iter().map(plane_layout).collect();
        let flags = if disjoint {
            vk::ImageCreateFlags::DISJOINT
        } else {
            vk::ImageCreateFlags::empty()
        };
        let image = self.create_import_image(
            vk::Format::G8_B8R8_2PLANE_420_UNORM,
            width,
            height,
            modifier,
            &layouts,
            flags,
        )?;

        if disjoint {
            let mut memories = Vec::new();
            for plane in plane_slice {
                let object_index = usize::try_from(plane.object_index).context("plane object")?;
                let object = object_slice.get(object_index).context("plane object bounds")?;
                memories.push(self.import_object_memory(object, vk::Image::null())?);
            }
            let mut plane_infos: Vec<vk::BindImagePlaneMemoryInfo> = [
                vk::ImageAspectFlags::MEMORY_PLANE_0_EXT,
                vk::ImageAspectFlags::MEMORY_PLANE_1_EXT,
            ]
            .into_iter()
            .map(|aspect| vk::BindImagePlaneMemoryInfo::default().plane_aspect(aspect))
            .collect();
            let mut binds: Vec<vk::BindImageMemoryInfo> = Vec::new();
            for (memory, plane_info) in memories.iter().zip(plane_infos.iter_mut()) {
                binds.push(
                    vk::BindImageMemoryInfo::default()
                        .image(image)
                        .memory(*memory)
                        .memory_offset(0)
                        .push_next(plane_info),
                );
            }
            unsafe { self.device.device().bind_image_memory2(&binds) }
                .context("bind disjoint image memory")?;
        } else {
            let object = object_slice.get(first_object).context("object bounds")?;
            let memory = self.import_object_memory(object, image)?;
            unsafe { self.device.device().bind_image_memory(image, memory, 0) }
                .context("bind imported image memory")?;
        }
        self.shape = ImportShape::TwoPlaneImage;
        Ok(())
    }

    /// Two single-plane layers: Y as R8, UV as GR88, each its own image.
    fn import_plane_layers(
        &mut self,
        object_slice: &[ff::AVDRMObjectDescriptor],
        layer_slice: &[ff::AVDRMLayerDescriptor],
        width: u32,
        height: u32,
    ) -> Result<()> {
        for (index, layer) in layer_slice.iter().enumerate() {
            // drivers disagree on the UV fourcc (mesa: GR88, the NVIDIA
            // shim: RG88); both are two-byte texels with U first in NV12
            let matches = if index == 0 {
                layer.format == DRM_FORMAT_R8
            } else {
                layer.format == DRM_FORMAT_GR88 || layer.format == DRM_FORMAT_RG88
            };
            ensure!(
                matches,
                "layer {index} format {:#x} does not match plane {index}",
                layer.format
            );
            let planes = usize::try_from(layer.nb_planes).context("layer planes")?;
            ensure!(planes == 1, "layer {index} has {planes} planes");
            let plane = layer.planes.first().context("plane bounds")?;
            let object_index = usize::try_from(plane.object_index).context("plane object")?;
            let object = object_slice.get(object_index).context("plane object bounds")?;

            let (format, plane_width, plane_height) = if index == 0 {
                (vk::Format::R8_UNORM, width, height)
            } else {
                (
                    vk::Format::R8G8_UNORM,
                    width.checked_div(2).unwrap_or(0),
                    height.checked_div(2).unwrap_or(0),
                )
            };
            let layouts = [plane_layout(plane)];
            let image = self.create_import_image(
                format,
                plane_width,
                plane_height,
                object.format_modifier,
                &layouts,
                vk::ImageCreateFlags::empty(),
            )?;
            let memory = self.import_object_memory(object, image)?;
            unsafe { self.device.device().bind_image_memory(image, memory, 0) }
                .context("bind imported plane memory")?;
        }
        self.shape = ImportShape::PerPlaneImages;
        Ok(())
    }

    /// Creates a dma-buf-importable image with the explicit modifier
    /// layout; the image is tracked for cleanup immediately.
    fn create_import_image(
        &mut self,
        format: vk::Format,
        width: u32,
        height: u32,
        modifier: u64,
        layouts: &[vk::SubresourceLayout],
        flags: vk::ImageCreateFlags,
    ) -> Result<vk::Image> {
        ensure!(width > 0 && height > 0, "imported plane has no size");
        let mut modifier_info = vk::ImageDrmFormatModifierExplicitCreateInfoEXT::default()
            .drm_format_modifier(modifier)
            .plane_layouts(layouts);
        let mut external_info = vk::ExternalMemoryImageCreateInfo::default()
            .handle_types(vk::ExternalMemoryHandleTypeFlags::DMA_BUF_EXT);
        let info = vk::ImageCreateInfo::default()
            .flags(flags)
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
        let image = unsafe { self.device.device().create_image(&info, None) }
            .with_context(|| format!("import {format:?} image (modifier {modifier:#x})"))?;
        self.images.push(image);
        Ok(image)
    }

    /// Imports one descriptor object's dma-buf as device memory (via a
    /// dup — the descriptor keeps ownership of the original fd). The
    /// memory is tracked for cleanup immediately.
    fn import_object_memory(
        &mut self,
        object: &ff::AVDRMObjectDescriptor,
        dedicated_image: vk::Image,
    ) -> Result<vk::DeviceMemory> {
        ensure!(object.fd >= 0, "descriptor object has no fd");
        let dup = unsafe { libc::fcntl(object.fd, libc::F_DUPFD_CLOEXEC, 0) };
        if dup < 0 {
            return Err(std::io::Error::last_os_error()).context("dup dma-buf fd");
        }
        // from here the dup either transfers into Vulkan (success) or
        // must be closed on every failure path
        let close_dup = || unsafe { libc::close(dup) };

        let mut fd_props = vk::MemoryFdPropertiesKHR::default();
        if let Err(e) = unsafe {
            self.device.external_memory_fd().get_memory_fd_properties(
                vk::ExternalMemoryHandleTypeFlags::DMA_BUF_EXT,
                dup,
                &mut fd_props,
            )
        } {
            close_dup();
            return Err(e).context("query dma-buf memory properties");
        }
        let type_index = match self
            .device
            .memory_type_index(fd_props.memory_type_bits, vk::MemoryPropertyFlags::DEVICE_LOCAL)
        {
            Ok(index) => index,
            Err(e) => {
                close_dup();
                return Err(e);
            }
        };

        let mut import_info = vk::ImportMemoryFdInfoKHR::default()
            .handle_type(vk::ExternalMemoryHandleTypeFlags::DMA_BUF_EXT)
            .fd(dup);
        let mut dedicated = vk::MemoryDedicatedAllocateInfo::default().image(dedicated_image);
        let object_size = match u64::try_from(object.size) {
            Ok(size) => size,
            Err(e) => {
                close_dup();
                return Err(e).context("object size");
            }
        };
        let mut alloc = vk::MemoryAllocateInfo::default()
            .allocation_size(object_size)
            .memory_type_index(type_index)
            .push_next(&mut import_info);
        if dedicated_image != vk::Image::null() {
            alloc = alloc.push_next(&mut dedicated);
        }
        match unsafe { self.device.device().allocate_memory(&alloc, None) } {
            Ok(memory) => {
                self.memories.push(memory);
                Ok(memory)
            }
            Err(e) => {
                close_dup();
                Err(e).context("import dma-buf memory")
            }
        }
    }


    /// Records the plane copies into the presentation images.
    fn record_copies(
        &self,
        vk_device: &ash::Device,
        cmd: vk::CommandBuffer,
        planes: &SizedPlanes,
        width: u32,
        height: u32,
    ) {
        let half_width = width.checked_div(2).unwrap_or(0);
        let half_height = height.checked_div(2).unwrap_or(0);
        let copy = |src_aspect, extent_w, extent_h| {
            vk::ImageCopy::default()
                .src_subresource(subresource_layers(src_aspect))
                .dst_subresource(subresource_layers(vk::ImageAspectFlags::COLOR))
                .extent(vk::Extent3D {
                    width: extent_w,
                    height: extent_h,
                    depth: 1,
                })
        };
        match &self.shape {
            ImportShape::TwoPlaneImage => {
                if let Some(&image) = self.images.first() {
                    unsafe {
                        vk_device.cmd_copy_image(
                            cmd,
                            image,
                            vk::ImageLayout::GENERAL,
                            planes.y.image,
                            vk::ImageLayout::GENERAL,
                            &[copy(vk::ImageAspectFlags::PLANE_0, width, height)],
                        );
                        vk_device.cmd_copy_image(
                            cmd,
                            image,
                            vk::ImageLayout::GENERAL,
                            planes.uv.image,
                            vk::ImageLayout::GENERAL,
                            &[copy(vk::ImageAspectFlags::PLANE_1, half_width, half_height)],
                        );
                    }
                }
            }
            ImportShape::PerPlaneImages => {
                let targets = [
                    (self.images.first(), planes.y.image, width, height),
                    (self.images.get(1), planes.uv.image, half_width, half_height),
                ];
                for (source, dest, extent_w, extent_h) in targets {
                    let Some(&source) = source else { continue };
                    unsafe {
                        vk_device.cmd_copy_image(
                            cmd,
                            source,
                            vk::ImageLayout::GENERAL,
                            dest,
                            vk::ImageLayout::GENERAL,
                            &[copy(vk::ImageAspectFlags::COLOR, extent_w, extent_h)],
                        );
                    }
                }
            }
        }
    }
}

impl Drop for ImportedSource {
    fn drop(&mut self) {
        unsafe {
            let vk_device = self.device.device();
            for &image in &self.images {
                vk_device.destroy_image(image, None);
            }
            for &memory in &self.memories {
                vk_device.free_memory(memory, None);
            }
        }
    }
}

const fn subresource_layers(aspect: vk::ImageAspectFlags) -> vk::ImageSubresourceLayers {
    vk::ImageSubresourceLayers {
        aspect_mask: aspect,
        mip_level: 0,
        base_array_layer: 0,
        layer_count: 1,
    }
}

/// The explicit-modifier layout of one descriptor plane.
fn plane_layout(plane: &ff::AVDRMPlaneDescriptor) -> vk::SubresourceLayout {
    vk::SubresourceLayout {
        offset: u64::try_from(plane.offset).unwrap_or(0),
        size: 0,
        row_pitch: u64::try_from(plane.pitch).unwrap_or(0),
        array_pitch: 0,
        depth_pitch: 0,
    }
}
