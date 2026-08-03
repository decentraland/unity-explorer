
use anyhow::{Result, anyhow};
use objc2_core_foundation::CFRetained;
use objc2_io_surface::IOSurfaceRef;
pub use uuav_abi::FrameInfo;

use crate::protocol::{MAX_PLANE_DIMENSION, SURFACE_SLOT_COUNT, SurfaceGeometry};

pub mod metal;
pub mod present;
pub mod synthetic;


pub use crate::protocol::assemble;


pub struct ImportedSurface {
    surface: CFRetained<IOSurfaceRef>,
    geometry: SurfaceGeometry,
    generation: u64,
    textures: metal::SurfaceTextures,
}

impl ImportedSurface {
    pub const fn geometry(&self) -> SurfaceGeometry {
        self.geometry
    }

    pub const fn generation(&self) -> u64 {
        self.generation
    }

    pub const fn textures(&self) -> &metal::SurfaceTextures {
        &self.textures
    }

    pub fn surface(&self) -> &IOSurfaceRef {
        &self.surface
    }
}

fn plane_extent(surface: &IOSurfaceRef, plane: usize) -> Result<(u32, u32)> {
    let declared = surface.plane_count();
    let (width, height) = match plane {
        0 if declared == 0 => (surface.width(), surface.height()),
        plane if plane < declared => (
            surface.width_of_plane(plane),
            surface.height_of_plane(plane),
        ),
        _ => (0, 0),
    };
    Ok((checked_extent(width)?, checked_extent(height)?))
}

fn checked_extent(value: usize) -> Result<u32> {
    let value = u32::try_from(value).map_err(|_| anyhow!("plane extent {value} out of range"))?;
    if value > MAX_PLANE_DIMENSION {
        return Err(anyhow!("plane extent {value} exceeds {MAX_PLANE_DIMENSION}"));
    }
    Ok(value)
}

pub fn measure(surface: &IOSurfaceRef) -> Result<SurfaceGeometry> {
    let (luma_width, luma_height) = plane_extent(surface, 0)?;
    let (chroma_width, chroma_height) = plane_extent(surface, 1)?;
    if luma_width == 0 || luma_height == 0 {
        return Err(anyhow!("surface luma plane is {luma_width}x{luma_height}"));
    }
    let declared = surface.plane_count();
    let plane_count = u32::try_from(declared.clamp(1, 2))
        .map_err(|_| anyhow!("implausible plane count {declared}"))?;
    Ok(SurfaceGeometry {
        plane_width: [luma_width, chroma_width],
        plane_height: [luma_height, chroma_height],
        plane_count,
    })
}

pub struct SurfaceTable {
    slots: [Option<ImportedSurface>; SURFACE_SLOT_COUNT],
}

impl Default for SurfaceTable {
    fn default() -> Self {
        Self::new()
    }
}

impl SurfaceTable {
    pub const fn new() -> Self {
        Self {
            slots: [const { None }; SURFACE_SLOT_COUNT],
        }
    }

    pub fn insert(
        &mut self,
        metal: &metal::MetalContext,
        slot: usize,
        surface: CFRetained<IOSurfaceRef>,
        generation: u64,
    ) -> Result<()> {
        if self.slots.get(slot).is_none() {
            return Err(anyhow!(
                "surface slot {slot} out of range (capacity {SURFACE_SLOT_COUNT})"
            ));
        }
        let geometry = measure(&surface)?;
        let textures = metal.wrap(&surface, &geometry)?;
        let entry = ImportedSurface {
            surface,
            geometry,
            generation,
            textures,
        };
        if let Some(cell) = self.slots.get_mut(slot) {
            *cell = Some(entry);
        }
        Ok(())
    }

    pub fn get(&self, slot: usize) -> Option<&ImportedSurface> {
        self.slots.get(slot)?.as_ref()
    }

    pub fn geometry(&self, slot: usize) -> Option<SurfaceGeometry> {
        Some(self.get(slot)?.geometry)
    }
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              exists to keep them out of the shipped code paths"
)]
mod tests {
    use super::*;

    #[test]
    fn checked_extent_rejects_absurd_planes() {
        assert_eq!(checked_extent(1920).unwrap(), 1920);
        assert!(checked_extent(MAX_PLANE_DIMENSION as usize).is_ok());
        assert!(checked_extent(MAX_PLANE_DIMENSION as usize + 1).is_err());
        assert!(checked_extent(usize::MAX).is_err());
    }
}
