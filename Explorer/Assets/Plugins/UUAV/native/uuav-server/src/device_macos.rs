//! The helper's own Metal device plus the 1x1 probe texture handed to the
//! core's `uuav_init`, which derives its device from a texture pointer —
//! the exact contract Unity fulfills in-process today.

use anyhow::{Context as _, Result};
use objc2::rc::Retained;
use objc2::runtime::ProtocolObject;
use objc2_metal::{
    MTLCommandQueue, MTLCreateSystemDefaultDevice, MTLDevice, MTLPixelFormat, MTLTexture,
    MTLTextureDescriptor,
};
use std::os::raw::c_void;

pub struct ProbeDevice {
    // field order = drop order: the probe must not outlive its device
    probe: Retained<ProtocolObject<dyn MTLTexture>>,
    /// The blit queue the video pump copies core frames to shared slots on.
    queue: Retained<ProtocolObject<dyn MTLCommandQueue>>,
    device: Retained<ProtocolObject<dyn MTLDevice>>,
}

impl ProbeDevice {
    /// `adapter` is the Metal `registryID` of Unity's device (0 = system
    /// default). On a mismatch (eGPU unplugged between sessions) this fails
    /// rather than silently decoding on a different GPU.
    pub fn new(adapter: u64) -> Result<Self> {
        let device = MTLCreateSystemDefaultDevice().context("no system default Metal device")?;

        if adapter != 0 && device.registryID() != adapter {
            // Apple Silicon machines expose exactly one GPU, so the default
            // device is the match in practice; refuse the exotic mismatch
            // instead of sharing surfaces across devices.
            anyhow::bail!(
                "default Metal device registryID {} does not match Unity's {adapter}",
                device.registryID()
            );
        }

        let descriptor = unsafe {
            MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
                MTLPixelFormat::RGBA8Unorm,
                1,
                1,
                false,
            )
        };
        let probe = device
            .newTextureWithDescriptor(&descriptor)
            .context("failed to create probe texture")?;
        let queue = device
            .newCommandQueue()
            .context("MTLDevice returned no command queue")?;

        Ok(Self { probe, queue, device })
    }

    /// The `id<MTLTexture>` pointer for the core's `uuav_init`.
    pub fn probe_ptr(&self) -> *const c_void {
        Retained::as_ptr(&self.probe).cast()
    }

    pub fn metal_device(&self) -> &ProtocolObject<dyn MTLDevice> {
        &self.device
    }

    pub fn command_queue(&self) -> &ProtocolObject<dyn MTLCommandQueue> {
        &self.queue
    }
}
