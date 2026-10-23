//! Bevy render plugin of the migration (BevyMigration/docs/README.md § 8): handle tables, a flat entity
//! set whose `GlobalTransform` is written straight from the snapshot (no `Transform`, no hierarchy), the
//! camera driven by Unity's matrices, and output into texture views the embedder supplies.
//!
//! Step 2 scope: every renderer is drawn as the built-in unit cube with one placeholder material; mesh,
//! texture and material upload are step 3.
#![cfg_attr(
    test,
    allow(clippy::expect_used, clippy::unwrap_used, clippy::float_cmp)
)]

pub mod readback;
pub mod snapshot;

use bevy::{
    prelude::*,
    render::camera::{
        ManualTextureView, ManualTextureViewHandle, ManualTextureViews, RenderTarget,
    },
};
use snapshot::Snapshot;
use std::collections::{HashMap, HashSet};

/// Texture views the camera renders into, one per output buffer; the embedder creates them from
/// whatever it shares with the host (`IOSurface` on macOS, plain textures for tests).
#[derive(Resource, Debug, Clone)]
pub struct OutputViews {
    pub handles: Vec<ManualTextureViewHandle>,
    pub size: UVec2,
}

/// The snapshot the next frame renders. Replaced wholesale by the embedder before each update.
#[derive(Resource, Debug, Default)]
pub struct PendingSnapshot(pub Option<Snapshot>);

/// Renderer handle to entity, the only persistent mapping (plan § 8 "Bevy side: handle tables").
#[derive(Resource, Debug, Default)]
pub struct HandleTable {
    entities: HashMap<u32, Entity>,
}

impl HandleTable {
    pub fn len(&self) -> usize {
        self.entities.len()
    }

    pub fn is_empty(&self) -> bool {
        self.entities.is_empty()
    }

    pub fn entity(&self, handle: u32) -> Option<Entity> {
        self.entities.get(&handle).copied()
    }
}

/// Marks the camera the snapshot drives.
#[derive(Component, Debug)]
pub struct BridgeCamera;

/// Marks the directional light the snapshot drives.
#[derive(Component, Debug)]
pub struct BridgeSun;

/// Marks an entity owned by a renderer handle.
#[derive(Component, Debug)]
pub struct BridgeRenderer {
    pub handle: u32,
}

#[derive(Resource, Debug)]
struct Placeholders {
    cube: Handle<Mesh>,
    material: Handle<StandardMaterial>,
}

/// Mirrors Z: Unity's left-handed world and Bevy's right-handed one differ by this reflection.
const MIRROR_Z: Mat4 = Mat4::from_cols(
    Vec4::new(1.0, 0.0, 0.0, 0.0),
    Vec4::new(0.0, 1.0, 0.0, 0.0),
    Vec4::new(0.0, 0.0, -1.0, 0.0),
    Vec4::new(0.0, 0.0, 0.0, 1.0),
);

#[derive(Debug, Default)]
pub struct BevyMigrationRenderPlugin;

impl Plugin for BevyMigrationRenderPlugin {
    fn build(&self, app: &mut App) {
        app.init_resource::<PendingSnapshot>()
            .init_resource::<HandleTable>()
            .init_resource::<CurrentFrame>()
            .add_systems(Startup, spawn_scene)
            .add_systems(PreUpdate, (apply_snapshot, select_output_view).chain());
    }
}

fn spawn_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<StandardMaterial>>,
) {
    commands.insert_resource(Placeholders {
        cube: meshes.add(Cuboid::new(1.0, 1.0, 1.0)),
        material: materials.add(StandardMaterial {
            base_color: Color::srgb(0.8, 0.5, 0.2),
            perceptual_roughness: 0.6,
            ..default()
        }),
    });

    commands.spawn((
        BridgeCamera,
        Camera3d::default(),
        Camera {
            clear_color: ClearColorConfig::Custom(Color::srgb(0.1, 0.12, 0.16)),
            ..default()
        },
        Projection::Perspective(PerspectiveProjection::default()),
        GlobalTransform::IDENTITY,
    ));

    commands.spawn((
        BridgeSun,
        DirectionalLight {
            illuminance: 10_000.0,
            shadows_enabled: false,
            ..default()
        },
        GlobalTransform::IDENTITY,
    ));
}

/// Moves the pending snapshot into entities: new handles spawn, missing handles despawn, the rest are
/// overwritten in place. Transforms are written as `GlobalTransform` directly.
fn apply_snapshot(
    mut commands: Commands,
    mut pending: ResMut<PendingSnapshot>,
    mut table: ResMut<HandleTable>,
    placeholders: Res<Placeholders>,
    mut ambient: ResMut<AmbientLight>,
    mut current_frame: ResMut<CurrentFrame>,
    mut cameras: Query<
        (&mut GlobalTransform, &mut Projection),
        (With<BridgeCamera>, Without<BridgeSun>),
    >,
    mut suns: Query<(&mut GlobalTransform, &mut DirectionalLight), With<BridgeSun>>,
    mut renderers: Query<
        (&mut GlobalTransform, &mut Visibility),
        (
            With<BridgeRenderer>,
            Without<BridgeCamera>,
            Without<BridgeSun>,
        ),
    >,
) {
    let Some(snapshot) = pending.0.take() else {
        return;
    };

    for (mut transform, mut projection) in &mut cameras {
        *transform = GlobalTransform::from(camera_world_matrix(&snapshot.camera.view));

        if let Projection::Perspective(perspective) = projection.as_mut() {
            perspective.fov = snapshot.camera.fov_degrees.to_radians();
            perspective.near = snapshot.camera.near;
            perspective.far = snapshot.camera.far;
        }
    }

    for (mut transform, mut light) in &mut suns {
        let direction = MIRROR_Z
            .transform_vector3(snapshot.sun_direction)
            .normalize_or_zero();

        if direction != Vec3::ZERO {
            *transform = GlobalTransform::from(Transform::from_rotation(Quat::from_rotation_arc(
                Vec3::NEG_Z,
                direction,
            )));
        }

        light.color = Color::linear_rgb(
            snapshot.sun_color.x,
            snapshot.sun_color.y,
            snapshot.sun_color.z,
        );
    }

    ambient.color = Color::linear_rgb(snapshot.ambient.x, snapshot.ambient.y, snapshot.ambient.z);

    let mut seen: HashSet<u32> = HashSet::with_capacity(snapshot.renderers.len());

    for renderer in &snapshot.renderers {
        seen.insert(renderer.handle);
        let world = GlobalTransform::from(renderer_world_matrix(&renderer.world));
        let visibility = if renderer.visible {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };

        if let Some(&entity) = table.entities.get(&renderer.handle) {
            if let Ok((mut transform, mut current)) = renderers.get_mut(entity) {
                *transform = world;
                *current = visibility;
            }
        } else {
            let entity = commands
                .spawn((
                    BridgeRenderer {
                        handle: renderer.handle,
                    },
                    Mesh3d(placeholders.cube.clone()),
                    MeshMaterial3d(placeholders.material.clone()),
                    world,
                    visibility,
                ))
                .id();
            table.entities.insert(renderer.handle, entity);
        }
    }

    table.entities.retain(|handle, entity| {
        if seen.contains(handle) {
            return true;
        }

        commands.entity(*entity).despawn();
        false
    });

    current_frame.0 = snapshot.frame;
}

/// The frame number the current update renders; the embedder reads it to pick the output buffer.
#[derive(Resource, Debug, Clone, Copy, Default)]
pub struct CurrentFrame(pub u64);

/// Points the camera at the output view of this frame's buffer.
fn select_output_view(
    frame: Option<Res<CurrentFrame>>,
    views: Option<Res<OutputViews>>,
    mut cameras: Query<&mut Camera, With<BridgeCamera>>,
) {
    let (Some(frame), Some(views)) = (frame, views) else {
        return;
    };

    if views.handles.is_empty() {
        return;
    }

    let index = usize::try_from(frame.0 % views.handles.len() as u64).unwrap_or(0);
    let Some(handle) = views.handles.get(index) else {
        return;
    };

    for mut camera in &mut cameras {
        camera.target = RenderTarget::TextureView(*handle);
    }
}

/// Registers `views` as the camera's output buffers.
pub fn install_output_views(
    world: &mut World,
    views: Vec<(ManualTextureViewHandle, ManualTextureView)>,
    size: UVec2,
) {
    let mut handles = Vec::with_capacity(views.len());
    let mut manual = world.resource_mut::<ManualTextureViews>();

    for (handle, view) in views {
        manual.insert(handle, view);
        handles.push(handle);
    }

    world.insert_resource(OutputViews { handles, size });
}

/// Unity's `worldToCameraMatrix` is already an OpenGL-style view (right-handed, -Z forward) of a
/// left-handed world. Bevy's camera lives in the mirrored world, so the camera transform is the
/// mirror of the inverse view.
pub fn camera_world_matrix(unity_view: &Mat4) -> Mat4 {
    MIRROR_Z * unity_view.inverse()
}

/// A left-handed local-to-world becomes right-handed by mirroring both its input and its output.
pub fn renderer_world_matrix(unity_local_to_world: &Mat4) -> Mat4 {
    MIRROR_Z * *unity_local_to_world * MIRROR_Z
}

/// Converts the ABI matrix (column-major, like `Matrix4x4`) to glam.
pub fn mat4_from_abi(m: &abi::BmMat4) -> Mat4 {
    Mat4::from_cols_array(&m.cols)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_unity_object_in_front_of_the_camera_stays_in_front() {
        // Unity: camera at origin looking +Z (left-handed), cube 5 m ahead at z = +5.
        let unity_view = Mat4::look_at_rh(Vec3::ZERO, Vec3::Z, Vec3::Y);
        let unity_cube = Mat4::from_translation(Vec3::new(0.0, 0.0, 5.0));

        let camera = camera_world_matrix(&unity_view);
        let cube = renderer_world_matrix(&unity_cube);

        let cube_in_view = camera.inverse() * cube;
        let position = cube_in_view.transform_point3(Vec3::ZERO);
        assert!(
            (position.z - -5.0).abs() < 1e-4,
            "cube must sit 5 m down Bevy's -Z, got {position}"
        );
    }

    #[test]
    fn renderer_matrix_keeps_scale_and_lateral_offsets() {
        let unity = Mat4::from_scale_rotation_translation(
            Vec3::new(2.0, 3.0, 4.0),
            Quat::IDENTITY,
            Vec3::new(1.0, 2.0, 3.0),
        );
        let bevy = renderer_world_matrix(&unity);
        let (scale, _, translation) = bevy.to_scale_rotation_translation();
        assert!((scale - Vec3::new(2.0, 3.0, 4.0)).length() < 1e-5);
        assert!((translation - Vec3::new(1.0, 2.0, -3.0)).length() < 1e-5);
    }
}
