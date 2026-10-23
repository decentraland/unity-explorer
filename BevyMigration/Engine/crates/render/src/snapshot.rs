//! Owned copy of one ABI snapshot (plan § 8 "Snapshot schema"), converted to glam once at the boundary.
//! Built from slices: turning the host's raw pointers into slices is the bridge's job, the one place
//! that may hold `unsafe` code.

use abi::{BmCamera, BmFog, BmLight, BmMat4, BmSkinned, MATERIAL_PLACEHOLDER, renderer_flags};
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
    pub cast_shadows: bool,
    /// `(bone_offset, bone_count)` into [`Snapshot::bones`] when the renderer is skinned.
    pub skin: Option<(u32, u32)>,
    /// One material handle per submesh; [`MATERIAL_PLACEHOLDER`] where the host uploaded none.
    pub materials: Vec<u32>,
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct SnapshotLight {
    pub kind: u32,
    pub shadows: bool,
    pub position: Vec3,
    pub direction: Vec3,
    pub color: Vec3,
    pub range: f32,
    pub spot_angle_degrees: f32,
}

impl SnapshotLight {
    pub fn from_abi(light: &BmLight) -> Self {
        Self {
            kind: light.kind,
            shadows: light.shadows != 0,
            position: Vec3::from_array(light.position),
            direction: Vec3::from_array(light.direction),
            color: Vec3::from_array(light.color),
            range: light.range,
            spot_angle_degrees: light.spot_angle,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct SnapshotFog {
    pub mode: u32,
    pub color: Vec3,
    pub density: f32,
    pub start: f32,
    pub end: f32,
}

impl SnapshotFog {
    pub fn from_abi(fog: &BmFog) -> Self {
        Self {
            mode: fog.mode,
            color: Vec3::from_array(fog.color),
            density: fog.density,
            start: fog.start,
            end: fog.end,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct Snapshot {
    pub frame: u64,
    pub camera: SnapshotCamera,
    pub ambient: Vec3,
    pub sun_direction: Vec3,
    pub sun_color: Vec3,
    pub fog: SnapshotFog,
    pub renderers: Vec<SnapshotRenderer>,
    pub lights: Vec<SnapshotLight>,
    /// Joint world matrices (Unity space) referenced by the skinned renderers.
    pub bones: Vec<Mat4>,
}

/// The per-renderer arrays of one snapshot, all of the same length, plus the flattened material handles.
#[derive(Debug, Clone, Copy)]
pub struct RendererArrays<'a> {
    pub handles: &'a [u32],
    pub worlds: &'a [BmMat4],
    pub meshes: &'a [u32],
    pub flags: &'a [u16],
    pub material_first: &'a [u32],
    pub material_count: &'a [u8],
    pub material_handles: &'a [u32],
    pub skinned: &'a [BmSkinned],
    pub bones: &'a [BmMat4],
}

/// The scalar parts of a snapshot.
#[derive(Debug, Clone, PartialEq)]
pub struct SnapshotHeader {
    pub frame: u64,
    pub camera: SnapshotCamera,
    pub ambient: Vec3,
    pub sun_direction: Vec3,
    pub sun_color: Vec3,
    pub fog: SnapshotFog,
}

impl Snapshot {
    /// Copies the arrays; returns None when their lengths disagree or a material range is out of bounds.
    /// Empty `material_first`/`material_count` arrays mean every renderer uses the placeholder material.
    pub fn new(
        header: SnapshotHeader,
        arrays: RendererArrays<'_>,
        lights: &[BmLight],
    ) -> Option<Self> {
        let count = arrays.handles.len();

        if arrays.worlds.len() != count
            || arrays.meshes.len() != count
            || arrays.flags.len() != count
        {
            return None;
        }

        let has_materials = !arrays.material_first.is_empty();

        if has_materials
            && (arrays.material_first.len() != count || arrays.material_count.len() != count)
        {
            return None;
        }

        let mut renderers = Vec::with_capacity(count);

        for i in 0..count {
            let materials = if has_materials {
                let first = usize::try_from(arrays.material_first[i]).ok()?;
                let end = first.checked_add(usize::from(arrays.material_count[i]))?;
                arrays.material_handles.get(first..end)?.to_vec()
            } else {
                vec![MATERIAL_PLACEHOLDER]
            };

            renderers.push(SnapshotRenderer {
                handle: arrays.handles[i],
                world: Mat4::from_cols_array(&arrays.worlds[i].cols),
                mesh: arrays.meshes[i],
                visible: arrays.flags[i] & renderer_flags::VISIBLE != 0,
                cast_shadows: arrays.flags[i] & renderer_flags::CAST_SHADOWS != 0,
                skin: None,
                materials,
            });
        }

        for skinned in arrays.skinned {
            let end = skinned.bone_offset.checked_add(skinned.bone_count)?;

            if usize::try_from(end).ok()? > arrays.bones.len() || skinned.bone_count == 0 {
                return None;
            }

            renderers
                .get_mut(usize::try_from(skinned.renderer_index).ok()?)?
                .skin = Some((skinned.bone_offset, skinned.bone_count));
        }

        Some(Self {
            frame: header.frame,
            camera: header.camera,
            ambient: header.ambient,
            sun_direction: header.sun_direction,
            sun_color: header.sun_color,
            fog: header.fog,
            renderers,
            lights: lights.iter().map(SnapshotLight::from_abi).collect(),
            bones: arrays
                .bones
                .iter()
                .map(|m| Mat4::from_cols_array(&m.cols))
                .collect(),
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use abi::fog_mode;

    fn header() -> SnapshotHeader {
        SnapshotHeader {
            frame: 7,
            camera: SnapshotCamera::from_abi(&BmCamera {
                view: BmMat4::IDENTITY,
                projection: BmMat4::IDENTITY,
                near: 0.1,
                far: 100.0,
                fov_degrees: 60.0,
            }),
            ambient: Vec3::ZERO,
            sun_direction: Vec3::NEG_Y,
            sun_color: Vec3::ONE,
            fog: SnapshotFog {
                mode: fog_mode::NONE,
                color: Vec3::ZERO,
                density: 0.0,
                start: 0.0,
                end: 0.0,
            },
        }
    }

    #[test]
    fn copies_every_renderer_and_decodes_flags_and_materials() {
        let arrays = RendererArrays {
            handles: &[3, 9],
            worlds: &[BmMat4::IDENTITY, BmMat4::IDENTITY],
            meshes: &[0, 5],
            flags: &[renderer_flags::VISIBLE | renderer_flags::CAST_SHADOWS, 0],
            material_first: &[0, 1],
            material_count: &[1, 2],
            material_handles: &[11, 12, 13],
            skinned: &[],
            bones: &[],
        };

        let snapshot = Snapshot::new(header(), arrays, &[]);

        assert!(
            snapshot.is_some(),
            "arrays of equal length make a valid snapshot"
        );
        let Some(snapshot) = snapshot else {
            return;
        };
        assert_eq!(snapshot.frame, 7);
        assert_eq!(snapshot.renderers.len(), 2);
        assert!(snapshot.renderers[0].visible && snapshot.renderers[0].cast_shadows);
        assert!(!snapshot.renderers[1].visible);
        assert_eq!(snapshot.renderers[0].materials, vec![11]);
        assert_eq!(snapshot.renderers[1].materials, vec![12, 13]);
        assert!((snapshot.camera.fov_degrees - 60.0).abs() < f32::EPSILON);
    }

    #[test]
    fn falls_back_to_the_placeholder_material_without_material_arrays() {
        let arrays = RendererArrays {
            handles: &[1],
            worlds: &[BmMat4::IDENTITY],
            meshes: &[0],
            flags: &[0],
            material_first: &[],
            material_count: &[],
            material_handles: &[],
            skinned: &[],
            bones: &[],
        };

        let snapshot = Snapshot::new(header(), arrays, &[]);

        assert!(snapshot.is_some_and(|s| s.renderers[0].materials == vec![MATERIAL_PLACEHOLDER]));
    }

    #[test]
    fn rejects_mismatched_lengths_and_material_ranges() {
        let short = RendererArrays {
            handles: &[1],
            worlds: &[],
            meshes: &[0],
            flags: &[0],
            material_first: &[],
            material_count: &[],
            material_handles: &[],
            skinned: &[],
            bones: &[],
        };
        assert!(Snapshot::new(header(), short, &[]).is_none());

        let out_of_range = RendererArrays {
            handles: &[1],
            worlds: &[BmMat4::IDENTITY],
            meshes: &[0],
            flags: &[0],
            material_first: &[2],
            material_count: &[1],
            material_handles: &[11],
            skinned: &[],
            bones: &[],
        };
        assert!(Snapshot::new(header(), out_of_range, &[]).is_none());
    }
}
