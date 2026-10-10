//! Unity-device capture from the probe texture (Metal).

use anyhow::{Context as _, Result, ensure};
use objc2::rc::Retained;
use objc2::runtime::ProtocolObject;
use objc2_metal::{MTLCommandQueue, MTLDevice, MTLResource as _, MTLTexture};
use std::os::raw::c_void;

/// Unity's Metal device, retained past the probe texture's lifetime, plus
/// the queue this dylib blits shared frames into presentation textures on.
pub struct UnityDevice {
    pub device: Retained<ProtocolObject<dyn MTLDevice>>,
    pub queue: Retained<ProtocolObject<dyn MTLCommandQueue>>,
    /// Sent to the helper so it decodes on the same GPU.
    pub registry_id: u64,
}

// Metal devices and command queues are free-threaded
unsafe impl Send for UnityDevice {}
unsafe impl Sync for UnityDevice {}

/// Captures the device behind Unity's probe texture (the probe itself is
/// destroyed by C# right after init).
///
/// # Safety
/// `texture` must be null (rejected) or a live `id<MTLTexture>`; there is
/// no QueryInterface analog to probe with.
pub unsafe fn capture_probe(texture: *const c_void) -> Result<UnityDevice> {
    ensure!(!texture.is_null(), "texture is null");
    let texture: &ProtocolObject<dyn MTLTexture> = unsafe { &*texture.cast() };
    let device = texture.device();
    let queue = device
        .newCommandQueue()
        .context("MTLDevice returned no command queue")?;
    let registry_id = device.registryID();
    Ok(UnityDevice {
        device,
        queue,
        registry_id,
    })
}
