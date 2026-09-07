//! The helper's own Vulkan device plus the probe pointer handed to the
//! core's `uuav_init`. Unlike the other platforms the probe is not a
//! texture: it names the [`HeadlessDevice`] itself (a `VkImage` carries
//! no device back-pointer), and the core adopts the device by `Arc`.

use anyhow::Result;
use std::os::raw::c_void;
use std::sync::Arc;
use uuav_core::linux_device::HeadlessDevice;

pub struct ProbeDevice {
    device: Arc<HeadlessDevice>,
}

impl ProbeDevice {
    /// `adapter` is the DRM dev_t of Unity's render node
    /// (`(major << 32) | minor`, 0 = first device with a render node).
    /// A mismatch fails rather than silently decoding on a different
    /// GPU — shared images cannot cross devices.
    pub fn new(adapter: u64) -> Result<Self> {
        Ok(Self {
            device: HeadlessDevice::new(adapter)?,
        })
    }

    /// The magic-tagged device pointer for the core's `uuav_init`.
    pub fn probe_ptr(&self) -> *const c_void {
        self.device.probe_ptr()
    }

    pub const fn headless(&self) -> &Arc<HeadlessDevice> {
        &self.device
    }
}
