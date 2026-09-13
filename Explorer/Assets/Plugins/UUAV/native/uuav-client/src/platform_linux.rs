//! Unity-device capture on Linux (Vulkan or OpenGL Core).
//!
//! A `VkImage` carries no device back-pointer, so unlike Metal/D3D11 the
//! probe texture cannot name Unity's device. Instead this dylib exports
//! `UnityPluginLoad` and captures Unity's plugin interfaces: which
//! renderer runs (`IUnityGraphics::GetRenderer`) and, under Vulkan, the
//! instance/device/queue (`IUnityGraphicsVulkan::Instance`). The probe
//! pointer is only null-checked. The FFI structs are hand mirrors of
//! the PluginAPI headers (`IUnityInterface.h`, `IUnityGraphics.h`,
//! `IUnityGraphicsVulkan.h`); on Linux `UNITY_INTERFACE_API` is the
//! plain C ABI.
//!
//! Copies recorded through [`VulkanDevice::with_command_buffer`] land in
//! Unity's currently recording command buffer
//! (`CommandRecordingState`), so they order naturally before the C#
//! blit that samples the presentation planes later in the same frame —
//! no queue submission from the plugin at all. The headless test
//! install swaps in an owned device + queue with a fenced submission
//! path instead.

use anyhow::{Context as _, Result, anyhow, bail, ensure};
use ash::vk;
use parking_lot::Mutex;
use std::os::raw::{c_char, c_void};
use std::sync::Arc;
use std::sync::atomic::{AtomicI32, AtomicPtr, Ordering};
use uuav_ipc::protocol::GraphicsApiWire;

// ---- PluginAPI mirrors (C ABI on Linux) --------------------------------

#[repr(C)]
struct UnityInterfacesRaw {
    get_interface: *const c_void,
    register_interface: *const c_void,
    get_interface_split: Option<unsafe extern "C" fn(u64, u64) -> *mut c_void>,
    register_interface_split: *const c_void,
}

/// `IUnityGraphics` prefix: only `GetRenderer` is consumed.
#[repr(C)]
struct UnityGraphicsRaw {
    get_renderer: Option<unsafe extern "C" fn() -> i32>,
}

const UNITY_GRAPHICS_GUID: (u64, u64) = (0x7CBA_0A9C_A4DD_B544, 0x8C5A_D492_6EB1_7B11);
const UNITY_GRAPHICS_VULKAN_GUID: (u64, u64) = (0x9535_5348_D4EF_4E11, 0x9789_313D_FCFF_CC87);

const RENDERER_OPENGL_CORE: i32 = 17;
const RENDERER_VULKAN: i32 = 21;

/// `UnityVulkanInstance`, field-for-field.
#[repr(C)]
struct UnityVulkanInstance {
    pipeline_cache: vk::PipelineCache,
    instance: vk::Instance,
    physical_device: vk::PhysicalDevice,
    device: vk::Device,
    graphics_queue: vk::Queue,
    get_instance_proc_addr:
        Option<unsafe extern "system" fn(vk::Instance, *const c_char) -> vk::PFN_vkVoidFunction>,
    queue_family_index: u32,
    reserved: [*mut c_void; 8],
}

/// `UnityVulkanRecordingState`, field-for-field.
#[repr(C)]
pub struct UnityVulkanRecordingState {
    pub command_buffer: vk::CommandBuffer,
    pub command_buffer_level: i32,
    pub render_pass: vk::RenderPass,
    pub framebuffer: vk::Framebuffer,
    pub sub_pass_index: i32,
    pub current_frame_number: u64,
    pub safe_frame_number: u64,
    pub reserved: [*mut c_void; 4],
}

const QUEUE_ACCESS_DONT_CARE: i32 = 0;

/// `IUnityGraphicsVulkan` vtable prefix, through the two render-pass
/// controls; the members between are never called and stay opaque.
#[repr(C)]
struct UnityGraphicsVulkanRaw {
    intercept_initialization: *const c_void,
    intercept_vulkan_api: *const c_void,
    configure_event: *const c_void,
    instance: Option<unsafe extern "C" fn() -> UnityVulkanInstance>,
    command_recording_state:
        Option<unsafe extern "C" fn(*mut UnityVulkanRecordingState, i32) -> bool>,
    access_texture: *const c_void,
    access_render_buffer_texture: *const c_void,
    access_render_buffer_resolve_texture: *const c_void,
    access_buffer: *const c_void,
    ensure_outside_render_pass: Option<unsafe extern "C" fn()>,
    ensure_inside_render_pass: *const c_void,
}

/// Captured at `UnityPluginLoad`; null until then (headless harnesses).
static UNITY_VULKAN_IFACE: AtomicPtr<UnityGraphicsVulkanRaw> =
    AtomicPtr::new(std::ptr::null_mut());
/// `UnityGfxRenderer` of the running engine; -1 = plugin not loaded.
static UNITY_RENDERER: AtomicI32 = AtomicI32::new(-1);

/// Called by Unity's plugin loader when this dylib is loaded.
///
/// # Safety
/// `interfaces` comes from Unity and follows the PluginAPI layout.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn UnityPluginLoad(interfaces: *mut c_void) {
    if interfaces.is_null() {
        return;
    }
    let interfaces = unsafe { &*interfaces.cast::<UnityInterfacesRaw>() };
    let Some(get_split) = interfaces.get_interface_split else {
        return;
    };
    let graphics = unsafe { get_split(UNITY_GRAPHICS_GUID.0, UNITY_GRAPHICS_GUID.1) };
    if !graphics.is_null() {
        let graphics = unsafe { &*graphics.cast::<UnityGraphicsRaw>() };
        if let Some(get_renderer) = graphics.get_renderer {
            UNITY_RENDERER.store(unsafe { get_renderer() }, Ordering::Release);
        }
    }
    let vulkan =
        unsafe { get_split(UNITY_GRAPHICS_VULKAN_GUID.0, UNITY_GRAPHICS_VULKAN_GUID.1) };
    UNITY_VULKAN_IFACE.store(vulkan.cast(), Ordering::Release);
}

#[unsafe(no_mangle)]
pub extern "C" fn UnityPluginUnload() {
    UNITY_VULKAN_IFACE.store(std::ptr::null_mut(), Ordering::Release);
    UNITY_RENDERER.store(-1, Ordering::Release);
}

// ---- the captured device ----------------------------------------------

/// Unity's graphics device, per API. Sent adapter + export flavor to
/// the helper; the present backends copy through it.
pub enum UnityDevice {
    Vulkan(Box<VulkanDevice>),
    OpenGl(GlDevice),
}

impl UnityDevice {
    /// The DRM dev_t of the device's render node (Vulkan; exact), 0 on
    /// GL (best-effort: the helper takes the first render node).
    pub const fn adapter(&self) -> u64 {
        match self {
            Self::Vulkan(device) => device.adapter,
            Self::OpenGl(_) => 0,
        }
    }

    pub const fn graphics_wire(&self) -> GraphicsApiWire {
        match self {
            Self::Vulkan(_) => GraphicsApiWire::Vulkan,
            Self::OpenGl(_) => GraphicsApiWire::OpenGl,
        }
    }
}

/// GL needs nothing captured up front: the render event runs with the
/// context current and the entry points load lazily there.
pub struct GlDevice {}

/// How Vulkan copies reach the GPU.
enum Submission {
    /// Record into Unity's current command buffer; Unity submits, and
    /// in-buffer order sequences the copy before C#'s sampling blit.
    UnityRecording,
    /// Headless (examples): own queue, fenced synchronously.
    Own {
        queue: Mutex<vk::Queue>,
        pool: vk::CommandPool,
        buffer: vk::CommandBuffer,
        fence: vk::Fence,
    },
}

pub struct VulkanDevice {
    pub device: ash::Device,
    pub queue_family: u32,
    pub external_memory_fd: ash::khr::external_memory_fd::Device,
    pub memory_props: vk::PhysicalDeviceMemoryProperties,
    pub adapter: u64,
    submission: Submission,
    instance: ash::Instance,
    /// Headless only: the owned handles to destroy on drop (Unity's are
    /// never ours to destroy).
    owned: Option<ash::Entry>,
}

// handles are freely shareable; the one externally-synchronized object
// (the Own queue) is behind its mutex, and Unity's recording state is
// only touched on Unity's render thread
unsafe impl Send for VulkanDevice {}
unsafe impl Sync for VulkanDevice {}

impl VulkanDevice {
    /// First suitable memory type for `type_bits`, preferring the
    /// requested flags.
    pub fn memory_type_index(&self, type_bits: u32, flags: vk::MemoryPropertyFlags) -> Result<u32> {
        let count = self
            .memory_props
            .memory_type_count
            .min(vk::MAX_MEMORY_TYPES as u32);
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

    /// Runs `record` against a command buffer that is guaranteed to
    /// execute before anything Unity records after this call returns.
    /// The Own-queue guard spans the whole call on purpose: it is the
    /// submission serialization.
    #[allow(clippy::significant_drop_tightening)]
    pub fn with_command_buffer(
        &self,
        record: impl FnOnce(&ash::Device, vk::CommandBuffer) -> Result<()>,
    ) -> Result<()> {
        match &self.submission {
            Submission::UnityRecording => {
                let iface = UNITY_VULKAN_IFACE.load(Ordering::Acquire);
                ensure!(!iface.is_null(), "IUnityGraphicsVulkan is gone");
                let iface = unsafe { &*iface };
                // copies are invalid inside a render pass
                if let Some(ensure_outside) = iface.ensure_outside_render_pass {
                    unsafe { ensure_outside() };
                }
                let recording = iface
                    .command_recording_state
                    .context("CommandRecordingState is missing")?;
                let mut state: UnityVulkanRecordingState = unsafe { std::mem::zeroed() };
                ensure!(
                    unsafe { recording(&mut state, QUEUE_ACCESS_DONT_CARE) },
                    "Unity has no recording command buffer"
                );
                ensure!(
                    state.command_buffer != vk::CommandBuffer::null(),
                    "Unity recording state has no command buffer"
                );
                record(&self.device, state.command_buffer)
            }
            Submission::Own {
                queue,
                pool,
                buffer,
                fence,
            } => {
                let queue = queue.lock();
                unsafe {
                    self.device
                        .reset_command_pool(*pool, vk::CommandPoolResetFlags::empty())
                        .context("reset command pool")?;
                    let begin = vk::CommandBufferBeginInfo::default()
                        .flags(vk::CommandBufferUsageFlags::ONE_TIME_SUBMIT);
                    self.device
                        .begin_command_buffer(*buffer, &begin)
                        .context("begin command buffer")?;
                }
                record(&self.device, *buffer)?;
                unsafe {
                    self.device
                        .end_command_buffer(*buffer)
                        .context("end command buffer")?;
                    self.device.reset_fences(&[*fence]).context("reset fence")?;
                    let buffers = [*buffer];
                    let submit = vk::SubmitInfo::default().command_buffers(&buffers);
                    self.device
                        .queue_submit(*queue, &[submit], *fence)
                        .context("queue submit")?;
                    self.device
                        .wait_for_fences(&[*fence], true, u64::MAX)
                        .context("wait for fence")?;
                }
                Ok(())
            }
        }
    }

    /// Builds the device view from Unity's captured Vulkan interface.
    fn from_unity() -> Result<Self> {
        let iface = UNITY_VULKAN_IFACE.load(Ordering::Acquire);
        ensure!(
            !iface.is_null(),
            "Unity did not provide IUnityGraphicsVulkan (was UnityPluginLoad called?)"
        );
        let iface_ref = unsafe { &*iface };
        let instance_fn = iface_ref
            .instance
            .context("IUnityGraphicsVulkan::Instance is missing")?;
        let unity = unsafe { instance_fn() };
        ensure!(
            unity.instance != vk::Instance::null() && unity.device != vk::Device::null(),
            "Unity's Vulkan device is not initialized yet"
        );
        let gipa = unity
            .get_instance_proc_addr
            .context("Unity passed no vkGetInstanceProcAddr")?;

        let static_fn = ash::StaticFn {
            get_instance_proc_addr: gipa,
        };
        let instance = unsafe { ash::Instance::load(&static_fn, unity.instance) };
        let device = unsafe { ash::Device::load(instance.fp_v1_0(), unity.device) };
        let external_memory_fd = ash::khr::external_memory_fd::Device::new(&instance, &device);
        let memory_props =
            unsafe { instance.get_physical_device_memory_properties(unity.physical_device) };
        let adapter = drm_adapter(&instance, unity.physical_device);

        Ok(Self {
            device,
            queue_family: unity.queue_family_index,
            external_memory_fd,
            memory_props,
            adapter,
            submission: Submission::UnityRecording,
            instance,
            owned: None,
        })
    }

    /// Headless device for the examples: own instance/device/queue with
    /// fenced submission, installed via [`test_install_headless_device`].
    fn headless() -> Result<Self> {
        let entry = unsafe { ash::Entry::load() }.context("load libvulkan")?;
        let app_info = vk::ApplicationInfo::default()
            .application_name(c"uuav-headless")
            .api_version(vk::API_VERSION_1_1);
        let instance_info = vk::InstanceCreateInfo::default().application_info(&app_info);
        let instance = unsafe { entry.create_instance(&instance_info, None) }
            .context("create Vulkan instance")?;

        let build = |instance: &ash::Instance| -> Result<(vk::PhysicalDevice, ash::Device, u32)> {
            let physicals = unsafe { instance.enumerate_physical_devices() }
                .context("enumerate physical devices")?;
            let mut chosen = None;
            for physical in physicals {
                let extensions =
                    unsafe { instance.enumerate_device_extension_properties(physical) }
                        .context("enumerate device extensions")?;
                let has = |name: &std::ffi::CStr| {
                    extensions.iter().any(|ext| {
                        ext.extension_name_as_c_str().is_ok_and(|ext_name| ext_name == name)
                    })
                };
                if has(ash::khr::external_memory_fd::NAME)
                    && has(ash::ext::external_memory_dma_buf::NAME)
                    && has(ash::ext::image_drm_format_modifier::NAME)
                {
                    chosen = Some(physical);
                    break;
                }
            }
            let physical = chosen.context("no dma-buf-capable Vulkan device")?;
            let families =
                unsafe { instance.get_physical_device_queue_family_properties(physical) };
            let queue_family = families
                .iter()
                .position(|family| family.queue_flags.contains(vk::QueueFlags::GRAPHICS))
                .context("no graphics queue family")?;
            let queue_family = u32::try_from(queue_family).context("queue family index")?;

            let extensions = [
                ash::khr::external_memory_fd::NAME.as_ptr(),
                ash::ext::external_memory_dma_buf::NAME.as_ptr(),
                ash::ext::image_drm_format_modifier::NAME.as_ptr(),
            ];
            let priorities = [1.0f32];
            let queue_info = vk::DeviceQueueCreateInfo::default()
                .queue_family_index(queue_family)
                .queue_priorities(&priorities);
            let queue_infos = [queue_info];
            let device_info = vk::DeviceCreateInfo::default()
                .queue_create_infos(&queue_infos)
                .enabled_extension_names(&extensions);
            let device = unsafe { instance.create_device(physical, &device_info, None) }
                .context("create Vulkan device")?;
            Ok((physical, device, queue_family))
        };
        let (physical, device, queue_family) = match build(&instance) {
            Ok(parts) => parts,
            Err(e) => {
                unsafe { instance.destroy_instance(None) };
                return Err(e);
            }
        };

        let queue = unsafe { device.get_device_queue(queue_family, 0) };
        let (pool, buffer, fence) = match own_commands(&device, queue_family) {
            Ok(parts) => parts,
            Err(e) => {
                unsafe {
                    device.destroy_device(None);
                    instance.destroy_instance(None);
                }
                return Err(e);
            }
        };

        let external_memory_fd = ash::khr::external_memory_fd::Device::new(&instance, &device);
        let memory_props = unsafe { instance.get_physical_device_memory_properties(physical) };
        let adapter = drm_adapter(&instance, physical);
        Ok(Self {
            device,
            queue_family,
            external_memory_fd,
            memory_props,
            adapter,
            submission: Submission::Own {
                queue: Mutex::new(queue),
                pool,
                buffer,
                fence,
            },
            instance,
            owned: Some(entry),
        })
    }
}

/// The Own-submission plumbing for the headless device.
fn own_commands(
    device: &ash::Device,
    queue_family: u32,
) -> Result<(vk::CommandPool, vk::CommandBuffer, vk::Fence)> {
    unsafe {
        let pool_info = vk::CommandPoolCreateInfo::default().queue_family_index(queue_family);
        let pool = device
            .create_command_pool(&pool_info, None)
            .context("create command pool")?;
        let alloc = vk::CommandBufferAllocateInfo::default()
            .command_pool(pool)
            .level(vk::CommandBufferLevel::PRIMARY)
            .command_buffer_count(1);
        let buffer = match device.allocate_command_buffers(&alloc) {
            Ok(buffers) => buffers.first().copied(),
            Err(e) => {
                device.destroy_command_pool(pool, None);
                return Err(e).context("allocate command buffer");
            }
        };
        let Some(buffer) = buffer else {
            device.destroy_command_pool(pool, None);
            bail!("no command buffer");
        };
        let fence = match device.create_fence(&vk::FenceCreateInfo::default(), None) {
            Ok(fence) => fence,
            Err(e) => {
                device.destroy_command_pool(pool, None);
                return Err(e).context("create fence");
            }
        };
        Ok((pool, buffer, fence))
    }
}

impl Drop for VulkanDevice {
    fn drop(&mut self) {
        if self.owned.is_some() {
            unsafe {
                let _ = self.device.device_wait_idle();
                if let Submission::Own {
                    pool, fence, ..
                } = &self.submission
                {
                    self.device.destroy_fence(*fence, None);
                    self.device.destroy_command_pool(*pool, None);
                }
                self.device.destroy_device(None);
                self.instance.destroy_instance(None);
            }
        }
    }
}

/// `(major << 32) | minor` of the physical device's render node, via
/// `VK_EXT_physical_device_drm`; 0 when unavailable.
fn drm_adapter(instance: &ash::Instance, physical: vk::PhysicalDevice) -> u64 {
    let has_ext = unsafe { instance.enumerate_device_extension_properties(physical) }
        .is_ok_and(|extensions| {
            extensions.iter().any(|ext| {
                ext.extension_name_as_c_str()
                    .is_ok_and(|name| name == ash::ext::physical_device_drm::NAME)
            })
        });
    if !has_ext {
        return 0;
    }
    let mut drm = vk::PhysicalDeviceDrmPropertiesEXT::default();
    let mut props = vk::PhysicalDeviceProperties2::default().push_next(&mut drm);
    unsafe { instance.get_physical_device_properties2(physical, &mut props) };
    if drm.has_render == 0 {
        return 0;
    }
    let major = u64::try_from(drm.render_major).unwrap_or(0);
    let minor = u64::try_from(drm.render_minor).unwrap_or(0);
    (major << 32) | minor
}

/// Headless override for the examples (no Unity, no plugin load).
static TEST_DEVICE: Mutex<Option<Arc<UnityDevice>>> = Mutex::new(None);

/// Installs an owned headless Vulkan device where `capture_probe` finds
/// it — the examples' stand-in for Unity's plugin-load capture.
pub fn test_install_headless_device() -> Result<()> {
    let device = VulkanDevice::headless()?;
    *TEST_DEVICE.lock() = Some(Arc::new(UnityDevice::Vulkan(Box::new(device))));
    Ok(())
}

/// Captures Unity's device. The probe pointer is only null-checked on
/// Linux (see the module docs); the API comes from the captured
/// renderer.
///
/// # Safety
/// No requirements beyond `texture` being the value C# passed.
pub unsafe fn capture_probe(texture: *const c_void) -> Result<UnityDevice> {
    ensure!(!texture.is_null(), "texture is null");
    let installed = TEST_DEVICE.lock().take();
    if let Some(installed) = installed {
        return Arc::try_unwrap(installed)
            .map_err(|_| anyhow!("headless test device is shared"));
    }
    match UNITY_RENDERER.load(Ordering::Acquire) {
        RENDERER_VULKAN => Ok(UnityDevice::Vulkan(Box::new(VulkanDevice::from_unity()?))),
        RENDERER_OPENGL_CORE => Ok(UnityDevice::OpenGl(GlDevice {})),
        -1 => bail!("Unity plugin interfaces were not captured (UnityPluginLoad has not run)"),
        other => bail!("unsupported graphics API (UnityGfxRenderer {other}); use Vulkan or OpenGL Core"),
    }
}
