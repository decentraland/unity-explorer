//! Unity-device introspection from the probe texture (Metal).

use anyhow::{Result, ensure};
use objc2::runtime::ProtocolObject;
use objc2_metal::{MTLDevice as _, MTLResource as _, MTLTexture};
use std::os::raw::c_void;

/// The `registryID` of the Metal device behind Unity's probe texture; the
/// helper must decode on the same GPU for IOSurface sharing to stay cheap.
///
/// # Safety
/// `texture` must be null (rejected) or a live `id<MTLTexture>`; there is
/// no QueryInterface analog to probe with.
pub unsafe fn adapter_of_probe(texture: *const c_void) -> Result<u64> {
    ensure!(!texture.is_null(), "texture is null");
    let texture: &ProtocolObject<dyn MTLTexture> = unsafe { &*texture.cast() };
    Ok(texture.device().registryID())
}
