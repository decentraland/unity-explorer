//! Client-side presentation: assembles shared-surface generations announced
//! by the helper, and on Unity's render event copies the latest published
//! slot into client-owned presentation planes on Unity's device — the same
//! stable-pointer contract (Y `R8Unorm`, UV `RG8Unorm`, retire-grace on
//! resolution change) the in-process plugin had, so the C# poll-and-rewrap
//! flow is untouched.

use crate::platform::UnityDevice;
use anyhow::{Context as _, Result, anyhow};
use objc2::rc::Retained;
use objc2::runtime::ProtocolObject;
use objc2_core_foundation::CFRetained;
use objc2_io_surface::IOSurfaceRef;
use objc2_metal::{
    MTLBlitCommandEncoder, MTLCommandBuffer, MTLCommandEncoder, MTLCommandQueue, MTLDevice,
    MTLOrigin, MTLPixelFormat, MTLSize, MTLStorageMode, MTLTexture, MTLTextureDescriptor,
    MTLTextureUsage,
};
use std::os::raw::c_void;
use uuav_ipc::mach_channel::SurfaceTag;

type Texture = Retained<ProtocolObject<dyn MTLTexture>>;

const SLOTS: usize = 3;
const PLANES: usize = 2;

/// A generation being assembled: surfaces arrive over the mach channel,
/// dimensions over zmq (`TextureSet`), in either order.
struct PendingGen {
    generation: u32,
    dims: Option<(u32, u32)>,
    surfaces: [[Option<CFRetained<IOSurfaceRef>>; PLANES]; SLOTS],
}

impl PendingGen {
    const fn empty(generation: u32) -> Self {
        Self {
            generation,
            dims: None,
            surfaces: [[None, None], [None, None], [None, None]],
        }
    }

    fn complete(&self) -> bool {
        self.dims.is_some()
            && self
                .surfaces
                .iter()
                .all(|slot| slot.iter().all(Option::is_some))
    }
}

/// An assembled generation: every slot surface wrapped on Unity's device.
struct ActiveGen {
    generation: u32,
    width: u32,
    height: u32,
    slots: [[Texture; PLANES]; SLOTS],
}

/// The presentation planes C# wraps, plus the size they were created for.
struct SizedPlanes {
    y: Texture,
    uv: Texture,
    width: u32,
    height: u32,
}

/// Per-player video state behind the mirror's mutex. Written by the IO and
/// mach threads (assembly, publishes), consumed on Unity's render thread.
#[derive(Default)]
pub struct PlayerVideo {
    pending: Option<PendingGen>,
    active: Option<ActiveGen>,
    presentation: Option<SizedPlanes>,
    /// Previous presentation generation, kept alive for one resolution
    /// change so the pointers C# still wraps stay valid until its next poll.
    retired: Option<SizedPlanes>,
    /// Newest (generation, slot) the helper finished writing.
    published: Option<(u32, u8)>,
    presented: Option<(u32, u8)>,
    /// Ack the render event owes the helper after wrapping a generation.
    ack_due: Option<u32>,
}

// Metal textures and IOSurfaces are free-threaded; everything here is
// accessed behind the mirror's mutex.
#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for PlayerVideo {}

impl PlayerVideo {
    /// One transferred surface from the mach channel.
    pub fn store_surface(&mut self, tag: &SurfaceTag, surface: CFRetained<IOSurfaceRef>) {
        let pending = self.pending_for(tag.generation);
        if let Some(cell) = pending
            .surfaces
            .get_mut(tag.slot as usize)
            .and_then(|slot| slot.get_mut(tag.plane as usize))
        {
            *cell = Some(surface);
        }
    }

    /// The zmq `TextureSet` announcement for a generation.
    pub fn store_texture_set(&mut self, generation: u32, width: u32, height: u32) {
        self.pending_for(generation).dims = Some((width, height));
    }

    pub const fn store_published(&mut self, generation: u32, slot: u8) {
        self.published = Some((generation, slot));
    }

    /// Drops all helper-side shared state after the helper died (surface
    /// retains release with it). The presentation planes and their retire
    /// grace survive so the pointers C# wraps stay valid — the last frame
    /// freezes until the resurrected helper publishes again.
    pub fn reset_for_recovery(&mut self) {
        self.pending = None;
        self.active = None;
        self.published = None;
        self.presented = None;
        self.ack_due = None;
    }

    fn pending_for(&mut self, generation: u32) -> &mut PendingGen {
        let stale = self
            .pending
            .as_ref()
            .is_some_and(|pending| pending.generation != generation);
        if stale || self.pending.is_none() {
            // a newer announcement supersedes whatever was half-assembled
            self.pending = Some(PendingGen::empty(generation));
        }
        // just ensured above
        self.pending.as_mut().unwrap_or_else(|| unreachable!())
    }

    /// The stable presentation-plane pointer C# wraps, once the first frame
    /// was presented.
    pub fn texture_ptr(&self, plane: i32) -> Result<*const c_void, String> {
        let planes = self
            .presentation
            .as_ref()
            .ok_or_else(|| "video texture is not available yet".to_owned())?;
        let texture = match plane {
            0 => &planes.y,
            1 => &planes.uv,
            _ => return Err(format!("unknown plane {plane}")),
        };
        Ok(Retained::as_ptr(texture).cast())
    }

    /// Render-thread presentation: wraps a completed pending generation,
    /// then copies the latest published slot into the presentation planes.
    /// Returns the generation to ack, if one was just wrapped.
    pub fn present(&mut self, unity: &UnityDevice) -> Result<Option<u32>> {
        let mut ack = None;
        if self.pending.as_ref().is_some_and(PendingGen::complete) {
            if let Some(pending) = self.pending.take() {
                self.active = Some(wrap_generation(unity, &pending)?);
                self.ack_due = Some(pending.generation);
            }
        }
        if self.ack_due.is_some() {
            ack = self.ack_due.take();
        }

        let Some((generation, slot)) = self.published else {
            return Ok(ack);
        };
        if self.presented == Some((generation, slot)) {
            return Ok(ack);
        }
        let Some((active_generation, width, height)) = self
            .active
            .as_ref()
            .map(|active| (active.generation, active.width, active.height))
        else {
            return Ok(ack);
        };
        if active_generation != generation {
            // publish for a generation this side hasn't wrapped (yet, or
            // anymore); the helper keeps publishing the acked one
            return Ok(ack);
        }

        self.ensure_presentation(unity, width, height)?;
        let Some(active) = self.active.as_ref() else {
            return Ok(ack);
        };
        let planes = self
            .presentation
            .as_ref()
            .ok_or_else(|| anyhow!("presentation planes are missing"))?;
        let sources = active
            .slots
            .get(slot as usize)
            .ok_or_else(|| anyhow!("published slot {slot} out of range"))?;

        blit_slot(unity, sources, planes)?;
        self.presented = Some((generation, slot));
        Ok(ack)
    }

    fn ensure_presentation(&mut self, unity: &UnityDevice, width: u32, height: u32) -> Result<()> {
        if self
            .presentation
            .as_ref()
            .is_some_and(|planes| planes.width == width && planes.height == height)
        {
            return Ok(());
        }
        let y = new_plane(unity, MTLPixelFormat::R8Unorm, width, height)?;
        let uv = new_plane(unity, MTLPixelFormat::RG8Unorm, width / 2, height / 2)?;
        // the generation before last dies here; C# has had a full poll
        // cycle to stop wrapping it
        let _previous = self.retired.take();
        self.retired = self.presentation.take();
        self.presentation = Some(SizedPlanes {
            y,
            uv,
            width,
            height,
        });
        Ok(())
    }
}

/// Wraps every surface of a completed pending generation on Unity's device.
fn wrap_generation(unity: &UnityDevice, pending: &PendingGen) -> Result<ActiveGen> {
    let (width, height) = pending.dims.ok_or_else(|| anyhow!("generation has no dims"))?;

    let wrap_slot = |slot: &[Option<CFRetained<IOSurfaceRef>>; PLANES]| -> Result<[Texture; PLANES]> {
        let y = slot[0].as_ref().ok_or_else(|| anyhow!("missing Y surface"))?;
        let uv = slot[1].as_ref().ok_or_else(|| anyhow!("missing UV surface"))?;
        Ok([
            wrap_surface(unity, y, MTLPixelFormat::R8Unorm)?,
            wrap_surface(unity, uv, MTLPixelFormat::RG8Unorm)?,
        ])
    };

    Ok(ActiveGen {
        generation: pending.generation,
        width,
        height,
        slots: [
            wrap_slot(&pending.surfaces[0])?,
            wrap_slot(&pending.surfaces[1])?,
            wrap_slot(&pending.surfaces[2])?,
        ],
    })
}

/// Client-side `MTLTexture` view over a whole single-plane shared surface.
fn wrap_surface(
    unity: &UnityDevice,
    surface: &IOSurfaceRef,
    format: MTLPixelFormat,
) -> Result<Texture> {
    let descriptor = unsafe {
        MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
            format,
            surface.width(),
            surface.height(),
            false,
        )
    };
    // IOSurface-backed textures must be Shared (they alias CPU-visible
    // memory); the default usage (ShaderRead) is fine for a blit source
    descriptor.setStorageMode(MTLStorageMode::Shared);
    unity
        .device
        .newTextureWithDescriptor_iosurface_plane(&descriptor, surface, 0)
        .ok_or_else(|| anyhow!("wrapping shared surface as {format:?} failed"))
}

/// One owned presentation plane C# samples from.
fn new_plane(
    unity: &UnityDevice,
    format: MTLPixelFormat,
    width: u32,
    height: u32,
) -> Result<Texture> {
    let descriptor = unsafe {
        MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
            format,
            width as usize,
            height as usize,
            false,
        )
    };
    descriptor.setUsage(MTLTextureUsage::ShaderRead);
    descriptor.setStorageMode(MTLStorageMode::Private);
    unity
        .device
        .newTextureWithDescriptor(&descriptor)
        .ok_or_else(|| anyhow!("newTexture({format:?} {width}x{height}) returned no texture"))
}

/// Copies one shared slot into the presentation planes, synchronously —
/// orders the copy against Unity's own queue exactly as the in-process
/// plugin did (`waitUntilCompleted` before C#'s blit samples the planes).
fn blit_slot(
    unity: &UnityDevice,
    sources: &[Texture; PLANES],
    planes: &SizedPlanes,
) -> Result<()> {
    let buffer = unity
        .queue
        .commandBuffer()
        .context("command queue returned no command buffer")?;
    let encoder = buffer
        .blitCommandEncoder()
        .context("command buffer returned no blit encoder")?;

    let origin = MTLOrigin { x: 0, y: 0, z: 0 };
    unsafe {
        encoder.copyFromTexture_sourceSlice_sourceLevel_sourceOrigin_sourceSize_toTexture_destinationSlice_destinationLevel_destinationOrigin(
            &sources[0],
            0,
            0,
            origin,
            MTLSize {
                width: planes.width as usize,
                height: planes.height as usize,
                depth: 1,
            },
            &planes.y,
            0,
            0,
            origin,
        );
        encoder.copyFromTexture_sourceSlice_sourceLevel_sourceOrigin_sourceSize_toTexture_destinationSlice_destinationLevel_destinationOrigin(
            &sources[1],
            0,
            0,
            origin,
            MTLSize {
                width: (planes.width / 2) as usize,
                height: (planes.height / 2) as usize,
                depth: 1,
            },
            &planes.uv,
            0,
            0,
            origin,
        );
    }
    encoder.endEncoding();
    buffer.commit();
    buffer.waitUntilCompleted();
    Ok(())
}
