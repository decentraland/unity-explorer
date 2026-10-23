//! Owned copy of one ABI snapshot (plan § 8 "Snapshot schema"), converted to glam once at the boundary.
//! Built from slices: turning the host's raw pointers into slices is the bridge's job, the one place
//! that may hold `unsafe` code.
#![cfg_attr(
    test,
    allow(clippy::expect_used, clippy::unwrap_used, clippy::float_cmp)
)]

use abi::{BmCamera, BmMat4, renderer_flags};
use bevy::prelude::*;

#[derive(Debug, Clone, PartialEq)]
pub struct SnapshotCamera {
    pub view: Mat4,
    pub projection: Mat4,
    pub near: f32,
    pub far: f32,
    pub fov_degrees: f32,
}

impl SnapshotCamera {
    pub fn from_abi(camera: &BmCamera) -> Self {
        Self {
            view: Mat4::from_cols_array(&camera.view.cols),
            projection: Mat4::from_cols_array(&camera.projection.cols),
            near: camera.near,
            far: camera.far,
            fov_degrees: camera.fov_degrees,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct SnapshotRenderer {
    pub handle: u32,
    pub world: Mat4,
    pub mesh: u32,
    pub visible: bool,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Snapshot {
    pub frame: u64,
    pub camera: SnapshotCamera,
    pub ambient: Vec3,
    pub sun_direction: Vec3,
    pub sun_color: Vec3,
    pub renderers: Vec<SnapshotRenderer>,
}

/// The per-renderer arrays of one snapshot, all of the same length.
#[derive(Debug, Clone, Copy)]
pub struct RendererArrays<'a> {
    pub handles: &'a [u32],
    pub worlds: &'a [BmMat4],
    pub meshes: &'a [u32],
    pub flags: &'a [u16],
}

impl Snapshot {
    /// Copies the arrays; returns None when their lengths disagree.
    pub fn new(
        frame: u64,
        camera: SnapshotCamera,
        ambient: Vec3,
        sun_direction: Vec3,
        sun_color: Vec3,
        arrays: RendererArrays<'_>,
    ) -> Option<Self> {
        let count = arrays.handles.len();

        if arrays.worlds.len() != count
            || arrays.meshes.len() != count
            || arrays.flags.len() != count
        {
            return None;
        }

        let mut renderers = Vec::with_capacity(count);

        for i in 0..count {
            renderers.push(SnapshotRenderer {
                handle: arrays.handles[i],
                world: Mat4::from_cols_array(&arrays.worlds[i].cols),
                mesh: arrays.meshes[i],
                visible: arrays.flags[i] & renderer_flags::VISIBLE != 0,
            });
        }

        Some(Self {
            frame,
            camera,
            ambient,
            sun_direction,
            sun_color,
            renderers,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn camera() -> SnapshotCamera {
        SnapshotCamera::from_abi(&BmCamera {
            view: BmMat4::IDENTITY,
            projection: BmMat4::IDENTITY,
            near: 0.1,
            far: 100.0,
            fov_degrees: 60.0,
        })
    }

    #[test]
    fn copies_every_renderer_and_decodes_flags() {
        let arrays = RendererArrays {
            handles: &[3, 9],
            worlds: &[BmMat4::IDENTITY, BmMat4::IDENTITY],
            meshes: &[0, 0],
            flags: &[renderer_flags::VISIBLE, 0],
        };

        let snapshot = Snapshot::new(7, camera(), Vec3::ZERO, Vec3::NEG_Y, Vec3::ONE, arrays)
            .expect("valid snapshot");

        assert_eq!(snapshot.frame, 7);
        assert_eq!(snapshot.renderers.len(), 2);
        assert!(snapshot.renderers[0].visible);
        assert!(!snapshot.renderers[1].visible);
        assert_eq!(snapshot.camera.fov_degrees, 60.0);
    }

    #[test]
    fn rejects_arrays_of_different_lengths() {
        let arrays = RendererArrays {
            handles: &[1],
            worlds: &[],
            meshes: &[0],
            flags: &[0],
        };

        assert!(Snapshot::new(1, camera(), Vec3::ZERO, Vec3::NEG_Y, Vec3::ONE, arrays).is_none());
    }
}
