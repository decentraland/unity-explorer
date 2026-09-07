//! Client-side presentation on Linux: assembles shared-image
//! generations announced by the helper (tagged fds over the surface
//! channel, layout over the control channel), and on Unity's render
//! event copies the latest published slot into client-owned
//! presentation planes on Unity's device — the same stable-pointer
//! contract (Y plane 0, UV plane 1, retire-grace on resolution change)
//! as the other platforms, so the C# poll-and-rewrap flow is untouched.
//!
//! The GPU work dispatches on the captured API: Vulkan imports the
//! dma-buf fds and records copies into Unity's command stream
//! (`present_vulkan`), GL imports opaque memory fds on the render
//! thread (`present_gl`).

use crate::platform::UnityDevice;
use crate::present_gl as gl;
use crate::present_vulkan as vulkan;
use anyhow::{Result, anyhow};
use std::sync::Arc;
use std::os::fd::OwnedFd;
use std::os::raw::c_void;
use uuav_ipc::fd_channel::SurfaceTag;
use uuav_ipc::protocol::{TextureImportWire, TexturePlaneWire};

pub const SLOTS: usize = 3;
pub const PLANES: usize = 2;

/// The `TextureSet` half of an announcement.
struct TexMeta {
    width: u32,
    height: u32,
    import: TextureImportWire,
    planes: Vec<TexturePlaneWire>,
}

/// A generation being assembled: fds arrive over the surface channel,
/// the layout over the control channel, in either order.
struct PendingGen {
    generation: u32,
    meta: Option<TexMeta>,
    fds: [[Option<OwnedFd>; PLANES]; SLOTS],
}

impl PendingGen {
    const fn empty(generation: u32) -> Self {
        Self {
            generation,
            meta: None,
            fds: [[None, None], [None, None], [None, None]],
        }
    }

    fn complete(&self) -> bool {
        self.meta.is_some() && self.fds.iter().all(|slot| slot.iter().all(Option::is_some))
    }
}

/// An assembled generation: every slot image imported on Unity's device.
struct ActiveGen {
    generation: u32,
    width: u32,
    height: u32,
    slots: BackendSlots,
}

enum BackendSlots {
    Vulkan(Box<vulkan::ImportedSlots>),
    Gl(gl::ImportedSlots),
}

/// The presentation planes C# wraps, per backend.
enum BackendPlanes {
    Vulkan(Box<vulkan::PresentationPlanes>),
    Gl(gl::PresentationPlanes),
}

impl BackendPlanes {
    const fn dims(&self) -> (u32, u32) {
        match self {
            Self::Vulkan(planes) => (planes.width, planes.height),
            Self::Gl(planes) => (planes.width, planes.height),
        }
    }
}

/// Per-player video state behind the mirror's mutex. Written by the IO
/// and surface threads (assembly, publishes), consumed on Unity's
/// render thread.
#[derive(Default)]
pub struct PlayerVideo {
    pending: Option<PendingGen>,
    active: Option<ActiveGen>,
    presentation: Option<BackendPlanes>,
    /// Previous presentation generation, kept alive for one resolution
    /// change so the pointers C# still wraps stay valid until its next
    /// poll.
    retired: Option<BackendPlanes>,
    /// Newest (generation, slot) the helper finished writing.
    published: Option<(u32, u8)>,
    presented: Option<(u32, u8)>,
    /// Ack the render event owes the helper after wrapping a generation.
    ack_due: Option<u32>,
}

impl PlayerVideo {
    /// One transferred fd from the surface channel.
    pub fn store_surface(&mut self, tag: &SurfaceTag, fd: OwnedFd) {
        let pending = self.pending_for(tag.generation);
        if let Some(cell) = pending
            .fds
            .get_mut(tag.slot as usize)
            .and_then(|slot| slot.get_mut(tag.plane as usize))
        {
            *cell = Some(fd);
        }
    }

    /// The control-channel `TextureSet` announcement for a generation.
    pub fn store_texture_set(
        &mut self,
        generation: u32,
        width: u32,
        height: u32,
        import: TextureImportWire,
        planes: Vec<TexturePlaneWire>,
    ) {
        self.pending_for(generation).meta = Some(TexMeta {
            width,
            height,
            import,
            planes,
        });
    }

    pub const fn store_published(&mut self, generation: u32, slot: u8) {
        self.published = Some((generation, slot));
    }

    /// Drops all helper-side shared state after the helper died (the
    /// imported images release with it). The presentation planes and
    /// their retire grace survive so the pointers C# wraps stay valid —
    /// the last frame freezes until the resurrected helper publishes
    /// again.
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

    /// The stable presentation pointer C# wraps, once the first frame
    /// was presented: `VkImage` handle (Vulkan) / GL texture name (GL).
    pub fn texture_ptr(&self, plane: i32) -> Result<*const c_void, String> {
        let planes = self
            .presentation
            .as_ref()
            .ok_or_else(|| "video texture is not available yet".to_owned())?;
        if !(0..=1).contains(&plane) {
            return Err(format!("unknown plane {plane}"));
        }
        Ok(match planes {
            BackendPlanes::Vulkan(planes) => planes.texture_ptr(plane),
            BackendPlanes::Gl(planes) => planes.texture_ptr(plane),
        })
    }

    /// Render-thread presentation: imports a completed pending
    /// generation, then copies the latest published slot into the
    /// presentation planes. Returns the generation to ack, if one was
    /// just wrapped.
    pub fn present(&mut self, unity: &Arc<UnityDevice>) -> Result<Option<u32>> {
        let mut ack = None;
        if self.pending.as_ref().is_some_and(PendingGen::complete)
            && let Some(pending) = self.pending.take()
        {
            self.active = Some(wrap_generation(unity, pending)?);
            self.ack_due = Some(
                self.active
                    .as_ref()
                    .map(|active| active.generation)
                    .unwrap_or_default(),
            );
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
        let Some(active) = self.active.as_ref() else {
            return Ok(ack);
        };
        if active.generation != generation {
            // publish for a generation this side hasn't wrapped (yet, or
            // anymore); the helper keeps publishing the acked one
            return Ok(ack);
        }
        let (width, height) = (active.width, active.height);

        self.ensure_presentation(unity, width, height)?;
        let Some(active) = self.active.as_ref() else {
            return Ok(ack);
        };
        let planes = self
            .presentation
            .as_mut()
            .ok_or_else(|| anyhow!("presentation planes are missing"))?;

        match (&active.slots, planes) {
            (BackendSlots::Vulkan(slots), BackendPlanes::Vulkan(planes)) => {
                let UnityDevice::Vulkan(device) = unity.as_ref() else {
                    return Err(anyhow!("backend/device mismatch"));
                };
                vulkan::copy_slot(device, slots, slot, planes)?;
            }
            (BackendSlots::Gl(slots), BackendPlanes::Gl(planes)) => {
                gl::copy_slot(slots, slot, planes)?;
            }
            _ => return Err(anyhow!("backend/device mismatch")),
        }
        self.presented = Some((generation, slot));
        Ok(ack)
    }

    fn ensure_presentation(
        &mut self,
        unity: &Arc<UnityDevice>,
        width: u32,
        height: u32,
    ) -> Result<()> {
        if self
            .presentation
            .as_ref()
            .is_some_and(|planes| planes.dims() == (width, height))
        {
            return Ok(());
        }
        let planes = match unity.as_ref() {
            UnityDevice::Vulkan(device) => BackendPlanes::Vulkan(Box::new(
                vulkan::PresentationPlanes::new(Arc::clone(unity), device, width, height)?,
            )),
            UnityDevice::OpenGl(_) => {
                BackendPlanes::Gl(gl::PresentationPlanes::new(width, height)?)
            }
        };
        // the generation before last dies here; C# has had a full poll
        // cycle to stop wrapping it
        let _previous = self.retired.take();
        self.retired = self.presentation.take();
        self.presentation = Some(planes);
        Ok(())
    }
}

/// Imports every fd of a completed pending generation on Unity's device.
fn wrap_generation(unity: &Arc<UnityDevice>, pending: PendingGen) -> Result<ActiveGen> {
    let generation = pending.generation;
    let meta = pending.meta.ok_or_else(|| anyhow!("generation has no layout"))?;
    let (width, height) = (meta.width, meta.height);
    let slots = match unity.as_ref() {
        UnityDevice::Vulkan(device) => BackendSlots::Vulkan(Box::new(vulkan::import_generation(
            Arc::clone(unity),
            device,
            meta.import,
            &meta.planes,
            pending.fds,
            width,
            height,
        )?)),
        UnityDevice::OpenGl(_) => BackendSlots::Gl(gl::import_generation(
            meta.import,
            &meta.planes,
            pending.fds,
            width,
            height,
        )?),
    };
    Ok(ActiveGen {
        generation,
        width,
        height,
        slots,
    })
}
