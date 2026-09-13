//! The one Vulkan device the whole helper shares on Linux.
//!
//! On macOS/Windows the engine hands the core a texture whose device
//! the core adopts. A `VkImage` carries no device back-pointer, so
//! Linux inverts the flow: the helper creates a [`HeadlessDevice`]
//! first, and the probe pointer handed to `uuav_init` is a magic-tagged
//! pointer to it ([`HeadlessDevice::probe_ptr`]).
//!
//! `HwDevice::from_texture` verifies the tag and clones the `Arc`.
//! Decode (VAAPI), the core's presentation blits, and the server's
//! shared-slot copies all run on this device, so images can flow
//! between them without cross-device export.
//!
//! `VkQueue` access requires external synchronization; every submitter
//! goes through [`HeadlessDevice::submit_and_wait`], which serializes on
//! the queue mutex and CPU-waits its fence (the Linux analog of the
//! Metal path's `waitUntilCompleted` stance).

use anyhow::{Context as _, Result, anyhow, ensure};
use ash::vk;
use parking_lot::Mutex;
use std::os::raw::c_void;
use std::sync::Arc;

/// First field of [`HeadlessDevice`]; lets `from_texture` reject any
/// pointer that is not ours with an actionable error instead of UB.
const MAGIC: u64 = 0x5555_4156_4C58_4456; // "UUAVLXDV"

/// Device extensions the import/export path cannot work without.
const REQUIRED_EXTENSIONS: [&std::ffi::CStr; 3] = [
    ash::khr::external_memory_fd::NAME,
    ash::ext::external_memory_dma_buf::NAME,
    ash::ext::image_drm_format_modifier::NAME,
];

#[repr(C)]
pub struct HeadlessDevice {
    /// Must stay the first field of this `#[repr(C)]` struct: the probe
    /// pointer contract reads it at offset 0 (never through the field —
    /// hence the dead-code allowance).
    #[allow(dead_code)]
    magic: u64,
    physical: vk::PhysicalDevice,
    queue: Mutex<vk::Queue>,
    queue_family: u32,
    memory_props: vk::PhysicalDeviceMemoryProperties,
    /// `(major << 32) | minor` of the render node, 0 when the device
    /// has none (e.g. llvmpipe).
    drm_dev: u64,
    /// `/dev/dri/renderD*` path backing this physical device, if any —
    /// what the VAAPI hwdevice opens.
    render_node: Option<String>,
    /// True when `VK_EXT_queue_family_foreign` is enabled (preferred
    /// source family for dma-buf acquire barriers).
    has_foreign_queue: bool,
    external_memory_fd: ash::khr::external_memory_fd::Device,
    image_drm_modifier: ash::ext::image_drm_format_modifier::Device,
    device: ash::Device,
    instance: ash::Instance,
    /// Keeps libvulkan loaded; dropped last.
    _entry: ash::Entry,
}

// Vulkan handles are freely shareable across threads; the queue - the
// one externally-synchronized object here - is behind its mutex.
unsafe impl Send for HeadlessDevice {}
unsafe impl Sync for HeadlessDevice {}

impl HeadlessDevice {
    /// Creates the device. `adapter` selects the GPU by DRM dev_t
    /// (`(major << 32) | minor` of its render node); 0 takes the first
    /// device with a render node, falling back to any device.
    pub fn new(adapter: u64) -> Result<Arc<Self>> {
        let entry = unsafe { ash::Entry::load() }.context("load libvulkan")?;

        let app_info = vk::ApplicationInfo::default()
            .application_name(c"uuav")
            .api_version(vk::API_VERSION_1_1);
        let instance_info = vk::InstanceCreateInfo::default().application_info(&app_info);
        let instance = unsafe { entry.create_instance(&instance_info, None) }
            .context("create Vulkan instance")?;
        // destroys the instance on any error path below; disarmed on
        // success when ownership moves into the struct
        let mut guard = InstanceGuard {
            instance: Some(instance),
        };
        let instance_ref = guard.instance.as_ref().context("instance guard")?;

        let selected = Self::pick_physical(instance_ref, adapter)?;
        let (device, queue, queue_family, has_foreign_queue) =
            Self::open_device_inner(instance_ref, selected)?;

        let external_memory_fd = ash::khr::external_memory_fd::Device::new(instance_ref, &device);
        let image_drm_modifier =
            ash::ext::image_drm_format_modifier::Device::new(instance_ref, &device);
        let memory_props =
            unsafe { instance_ref.get_physical_device_memory_properties(selected.physical) };
        let render_node = find_render_node(selected.drm_dev);
        let instance = guard.instance.take().context("instance guard")?;
        Ok(Arc::new(Self {
            magic: MAGIC,
            physical: selected.physical,
            queue: Mutex::new(queue),
            queue_family,
            memory_props,
            drm_dev: selected.drm_dev,
            render_node,
            has_foreign_queue,
            external_memory_fd,
            image_drm_modifier,
            device,
            instance,
            _entry: entry,
        }))
    }

    /// The pointer handed to `uuav_init` as the probe texture.
    pub fn probe_ptr(self: &Arc<Self>) -> *const c_void {
        Arc::as_ptr(self).cast()
    }

    /// Adopts a probe pointer produced by [`Self::probe_ptr`], adding a
    /// strong reference.
    ///
    /// # Safety
    /// `ptr` must be null (rejected), or a pointer obtained from
    /// [`Self::probe_ptr`] on a still-live `Arc`. The magic check turns
    /// accidents into errors, not guarantees.
    pub unsafe fn adopt_probe_ptr(ptr: *const c_void) -> Result<Arc<Self>> {
        ensure!(!ptr.is_null(), "texture is null");
        ensure!(
            ptr.align_offset(align_of::<u64>()) == 0,
            "probe pointer is misaligned"
        );
        let magic = unsafe { ptr.cast::<u64>().read() };
        ensure!(
            magic == MAGIC,
            "probe pointer is not a uuav HeadlessDevice (in-process Unity mode is unsupported on Linux)"
        );
        let ptr = ptr.cast::<Self>();
        unsafe { Arc::increment_strong_count(ptr) };
        Ok(unsafe { Arc::from_raw(ptr) })
    }

    pub const fn device(&self) -> &ash::Device {
        &self.device
    }

    pub const fn instance(&self) -> &ash::Instance {
        &self.instance
    }

    pub const fn physical(&self) -> vk::PhysicalDevice {
        self.physical
    }

    pub const fn queue_family(&self) -> u32 {
        self.queue_family
    }

    pub const fn drm_dev(&self) -> u64 {
        self.drm_dev
    }

    pub fn render_node(&self) -> Option<&str> {
        self.render_node.as_deref()
    }

    pub const fn external_memory_fd(&self) -> &ash::khr::external_memory_fd::Device {
        &self.external_memory_fd
    }

    pub const fn image_drm_modifier(&self) -> &ash::ext::image_drm_format_modifier::Device {
        &self.image_drm_modifier
    }

    /// The queue family index for dma-buf acquire barriers:
    /// `FOREIGN_EXT` when the extension is up, `EXTERNAL` otherwise.
    pub const fn external_src_family(&self) -> u32 {
        if self.has_foreign_queue {
            vk::QUEUE_FAMILY_FOREIGN_EXT
        } else {
            vk::QUEUE_FAMILY_EXTERNAL
        }
    }

    /// First suitable memory type for `type_bits`, preferring
    /// device-local.
    pub fn memory_type_index(&self, type_bits: u32, flags: vk::MemoryPropertyFlags) -> Result<u32> {
        let count = self.memory_props.memory_type_count.min(vk::MAX_MEMORY_TYPES as u32);
        let preferred = (0..count).find(|&index| {
            let supported = type_bits & (1u32 << index) != 0;
            let type_flags = self
                .memory_props
                .memory_types
                .get(index as usize)
                .map(|t| t.property_flags)
                .unwrap_or_default();
            supported && type_flags.contains(flags)
        });
        preferred
            .or_else(|| (0..count).find(|&index| type_bits & (1u32 << index) != 0))
            .ok_or_else(|| anyhow!("no memory type matches bits {type_bits:#x}"))
    }

    /// Records commands via `record`, submits, and CPU-waits the fence.
    /// One submitter at a time: the queue mutex is held for the whole
    /// call — that hold IS the submission serialization.
    #[allow(clippy::significant_drop_tightening)]
    pub fn submit_and_wait(
        &self,
        pool: vk::CommandPool,
        buffer: vk::CommandBuffer,
        fence: vk::Fence,
        record: impl FnOnce(vk::CommandBuffer) -> Result<()>,
    ) -> Result<()> {
        let queue = self.queue.lock();
        unsafe {
            self.device
                .reset_command_pool(pool, vk::CommandPoolResetFlags::empty())
                .context("reset command pool")?;
            let begin = vk::CommandBufferBeginInfo::default()
                .flags(vk::CommandBufferUsageFlags::ONE_TIME_SUBMIT);
            self.device
                .begin_command_buffer(buffer, &begin)
                .context("begin command buffer")?;
        }
        record(buffer)?;
        unsafe {
            self.device
                .end_command_buffer(buffer)
                .context("end command buffer")?;
            self.device
                .reset_fences(&[fence])
                .context("reset fence")?;
            let buffers = [buffer];
            let submit = vk::SubmitInfo::default().command_buffers(&buffers);
            self.device
                .queue_submit(*queue, &[submit], fence)
                .context("queue submit")?;
            self.device
                .wait_for_fences(&[fence], true, u64::MAX)
                .context("wait for fence")?;
        }
        Ok(())
    }

    fn pick_physical(instance: &ash::Instance, adapter: u64) -> Result<Selected> {
        let physicals = unsafe { instance.enumerate_physical_devices() }
            .context("enumerate physical devices")?;
        ensure!(!physicals.is_empty(), "no Vulkan devices present");

        let mut fallback: Option<Selected> = None;
        for physical in physicals {
            let mut drm = vk::PhysicalDeviceDrmPropertiesEXT::default();
            let mut props = vk::PhysicalDeviceProperties2::default().push_next(&mut drm);
            unsafe { instance.get_physical_device_properties2(physical, &mut props) };

            let has_drm_ext = Self::has_extension(
                instance,
                physical,
                ash::ext::physical_device_drm::NAME,
            )?;
            let drm_dev = if has_drm_ext && drm.has_render != 0 {
                Self::pack_dev(drm.render_major, drm.render_minor)?
            } else {
                0
            };

            let selected = Selected { physical, drm_dev };
            if adapter != 0 {
                if drm_dev == adapter {
                    return Ok(selected);
                }
                continue;
            }
            if drm_dev != 0 {
                return Ok(selected);
            }
            fallback.get_or_insert(selected);
        }
        if adapter != 0 {
            return Err(anyhow!(
                "no Vulkan device matches DRM adapter {adapter:#x}"
            ));
        }
        fallback.context("no usable Vulkan device")
    }

    /// Creates the logical device; returns (device, queue, family,
    /// foreign-queue-enabled).
    fn open_device_inner(
        instance: &ash::Instance,
        selected: Selected,
    ) -> Result<(ash::Device, vk::Queue, u32, bool)> {
        let physical = selected.physical;
        for name in REQUIRED_EXTENSIONS {
            ensure!(
                Self::has_extension(instance, physical, name)?,
                "Vulkan device lacks required extension {}",
                name.to_string_lossy()
            );
        }
        let has_foreign_queue =
            Self::has_extension(instance, physical, ash::ext::queue_family_foreign::NAME)?;

        let families =
            unsafe { instance.get_physical_device_queue_family_properties(physical) };
        let queue_family = families
            .iter()
            .position(|family| family.queue_flags.contains(vk::QueueFlags::GRAPHICS))
            .or_else(|| {
                families
                    .iter()
                    .position(|family| family.queue_flags.contains(vk::QueueFlags::TRANSFER))
            })
            .context("no graphics/transfer queue family")?;
        let queue_family = u32::try_from(queue_family).context("queue family index")?;

        let mut extensions: Vec<*const std::os::raw::c_char> =
            REQUIRED_EXTENSIONS.iter().map(|name| name.as_ptr()).collect();
        if has_foreign_queue {
            extensions.push(ash::ext::queue_family_foreign::NAME.as_ptr());
        }

        // multi-planar (NV12) image support rides the samplerYcbcrConversion
        // feature on several drivers; request it when present
        let mut ycbcr_query = vk::PhysicalDeviceSamplerYcbcrConversionFeatures::default();
        let mut features_query =
            vk::PhysicalDeviceFeatures2::default().push_next(&mut ycbcr_query);
        unsafe { instance.get_physical_device_features2(physical, &mut features_query) };
        let mut ycbcr_enable = vk::PhysicalDeviceSamplerYcbcrConversionFeatures::default()
            .sampler_ycbcr_conversion(ycbcr_query.sampler_ycbcr_conversion != 0);

        let priorities = [1.0f32];
        let queue_info = vk::DeviceQueueCreateInfo::default()
            .queue_family_index(queue_family)
            .queue_priorities(&priorities);
        let queue_infos = [queue_info];
        let device_info = vk::DeviceCreateInfo::default()
            .queue_create_infos(&queue_infos)
            .enabled_extension_names(&extensions)
            .push_next(&mut ycbcr_enable);

        let device = unsafe { instance.create_device(physical, &device_info, None) }
            .context("create Vulkan device")?;
        let queue = unsafe { device.get_device_queue(queue_family, 0) };
        Ok((device, queue, queue_family, has_foreign_queue))
    }

    fn has_extension(
        instance: &ash::Instance,
        physical: vk::PhysicalDevice,
        name: &std::ffi::CStr,
    ) -> Result<bool> {
        let extensions = unsafe { instance.enumerate_device_extension_properties(physical) }
            .context("enumerate device extensions")?;
        Ok(extensions.iter().any(|ext| {
            ext.extension_name_as_c_str()
                .is_ok_and(|ext_name| ext_name == name)
        }))
    }

    fn pack_dev(major: i64, minor: i64) -> Result<u64> {
        let major = u32::try_from(major).context("DRM render major")?;
        let minor = u32::try_from(minor).context("DRM render minor")?;
        Ok((u64::from(major) << 32) | u64::from(minor))
    }
}

impl Drop for HeadlessDevice {
    fn drop(&mut self) {
        // Destroyed exactly once, when the last shared reference goes.
        // The core joins its playback threads before releasing their
        // shares (see `UUAVPlayer::drop`), so this never races another
        // thread's use of the device; `device_wait_idle` then drains any
        // in-flight GPU work before teardown.
        unsafe {
            let _ = self.device.device_wait_idle();
            self.device.destroy_device(None);
            self.instance.destroy_instance(None);
        }
    }
}

#[derive(Clone, Copy)]
struct Selected {
    physical: vk::PhysicalDevice,
    drm_dev: u64,
}

/// Destroys a not-yet-adopted instance on early error returns.
struct InstanceGuard {
    instance: Option<ash::Instance>,
}

impl Drop for InstanceGuard {
    fn drop(&mut self) {
        if let Some(instance) = self.instance.take() {
            unsafe { instance.destroy_instance(None) };
        }
    }
}

/// Resolves a packed DRM dev_t to its `/dev/dri/renderD*` path by
/// stat-scanning the directory. `None` for 0 or when nothing matches.
fn find_render_node(drm_dev: u64) -> Option<String> {
    use std::os::unix::fs::MetadataExt as _;

    if drm_dev == 0 {
        return None;
    }
    let entries = std::fs::read_dir("/dev/dri").ok()?;
    for entry in entries.flatten() {
        let name = entry.file_name();
        let Some(name) = name.to_str() else { continue };
        if !name.starts_with("renderD") {
            continue;
        }
        let path = entry.path();
        let Ok(metadata) = std::fs::metadata(&path) else {
            continue;
        };
        let rdev = metadata.rdev();
        let major = libc::major(rdev);
        let minor = libc::minor(rdev);
        if (u64::from(major) << 32) | u64::from(minor) == drm_dev {
            return path.to_str().map(str::to_owned);
        }
    }
    None
}
