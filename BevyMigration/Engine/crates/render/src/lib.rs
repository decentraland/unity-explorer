//! Bevy render plugin of the migration (BevyMigration/docs/README.md § 8): handle tables, a flat entity
//! set whose `GlobalTransform` is written straight from the snapshot (no `Transform`, no hierarchy), the
//! camera driven by Unity's matrices, lights and fog from the snapshot, resources uploaded once from the
//! host, and output into texture views the embedder supplies.

pub mod materials;
pub mod readback;
pub mod resources;
pub mod snapshot;

use abi::{MATERIAL_PLACEHOLDER, MESH_PLACEHOLDER_CUBE, fog_mode, light_kind};
use bevy::{
    core_pipeline::tonemapping::Tonemapping,
    pbr::{DistanceFog, ExtendedMaterial, FogFalloff},
    prelude::*,
    render::camera::{
        ManualTextureView, ManualTextureViewHandle, ManualTextureViews, RenderTarget,
    },
    render::{camera::Exposure, mesh::skinning::SkinnedMesh},
};
use materials::{MaterialFamiliesPlugin, UrpLitExtension, UrpLitMaterial};
use resources::{PendingResources, ResourceTables, apply_resources};
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

/// Renderer handle to entities (one per submesh), the only persistent mapping (plan § 8).
#[derive(Resource, Debug, Default)]
pub struct HandleTable {
    entities: HashMap<u32, Vec<Entity>>,
}

impl HandleTable {
    pub fn len(&self) -> usize {
        self.entities.len()
    }

    pub fn is_empty(&self) -> bool {
        self.entities.is_empty()
    }

    pub fn entities(&self, handle: u32) -> Option<&[Entity]> {
        self.entities.get(&handle).map(Vec::as_slice)
    }
}

/// The frame number the current update renders; the embedder reads it to pick the output buffer.
#[derive(Resource, Debug, Clone, Copy, Default)]
pub struct CurrentFrame(pub u64);

/// Marks the camera the snapshot drives.
#[derive(Component, Debug)]
pub struct BridgeCamera;

/// Marks the directional light the snapshot's sun drives.
#[derive(Component, Debug)]
pub struct BridgeSun;

/// Marks a light spawned from the snapshot's light array, by index.
#[derive(Component, Debug)]
pub struct BridgeLight {
    pub index: usize,
}

/// A joint entity of a skinned renderer; its `GlobalTransform` is written from the snapshot's bones.
#[derive(Component, Debug)]
pub struct BridgeJoint;

/// Joint entity sets keyed by their `bone_offset` in the snapshot, shared by every renderer skinned
/// to the same skeleton.
#[derive(Resource, Default, Debug)]
pub struct JointSets(HashMap<u32, Vec<Entity>>);

/// Marks an entity owned by a renderer handle.
#[derive(Component, Debug)]
pub struct BridgeRenderer {
    pub handle: u32,
    pub submesh: usize,
    /// `(bone_offset, bone_count)` the entity's `SkinnedMesh` was built from.
    pub skin: Option<(u32, u32)>,
}

#[derive(Resource, Debug)]
struct Placeholders {
    cube: Handle<Mesh>,
    material: Handle<UrpLitMaterial>,
}

#[derive(Resource, Debug, Default)]
struct LightEntities(Vec<Entity>);

/// Mirrors Z: Unity's left-handed world and Bevy's right-handed one differ by this reflection.
pub(crate) const MIRROR_Z: Mat4 = Mat4::from_cols(
    Vec4::new(1.0, 0.0, 0.0, 0.0),
    Vec4::new(0.0, 1.0, 0.0, 0.0),
    Vec4::new(0.0, 0.0, -1.0, 0.0),
    Vec4::new(0.0, 0.0, 0.0, 1.0),
);

#[derive(Debug, Default)]
pub struct BevyMigrationRenderPlugin;

impl Plugin for BevyMigrationRenderPlugin {
    fn build(&self, app: &mut App) {
        app.add_plugins(MaterialFamiliesPlugin)
            .init_resource::<PendingSnapshot>()
            .init_resource::<PendingResources>()
            .init_resource::<ResourceTables>()
            .init_resource::<HandleTable>()
            .init_resource::<CurrentFrame>()
            .init_resource::<LightEntities>()
            .init_resource::<JointSets>()
            .add_systems(Startup, spawn_scene)
            .add_systems(
                PreUpdate,
                (apply_resources, apply_snapshot, select_output_view).chain(),
            );
    }
}

fn spawn_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<UrpLitMaterial>>,
) {
    commands.insert_resource(Placeholders {
        cube: meshes.add(Cuboid::new(1.0, 1.0, 1.0)),
        material: materials.add(ExtendedMaterial {
            base: StandardMaterial {
                base_color: Color::srgb(0.8, 0.5, 0.2),
                perceptual_roughness: 0.6,
                ..default()
            },
            extension: UrpLitExtension::default(),
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
        DistanceFog::default(),
        // Unity's volume stack tone maps after the composite; Bevy outputs scene-linear encoded as sRGB.
        Tonemapping::None,
        UNITY_EXPOSURE,
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
    tables: Res<ResourceTables>,
    placeholders: Res<Placeholders>,
    mut ambient: ResMut<AmbientLight>,
    mut current_frame: ResMut<CurrentFrame>,
    mut light_entities: ResMut<LightEntities>,
    mut joint_sets: ResMut<JointSets>,
    mut cameras: Query<
        (&mut GlobalTransform, &mut Projection, &mut DistanceFog),
        (With<BridgeCamera>, Without<BridgeSun>, Without<BridgeJoint>),
    >,
    mut suns: Query<
        (&mut GlobalTransform, &mut DirectionalLight),
        (With<BridgeSun>, Without<BridgeJoint>),
    >,
    mut joints: Query<
        &mut GlobalTransform,
        (
            With<BridgeJoint>,
            Without<BridgeCamera>,
            Without<BridgeSun>,
            Without<BridgeRenderer>,
        ),
    >,
    mut renderers: Query<
        (
            &mut GlobalTransform,
            &mut Visibility,
            &mut Mesh3d,
            &mut MeshMaterial3d<UrpLitMaterial>,
            &mut BridgeRenderer,
        ),
        (
            Without<BridgeCamera>,
            Without<BridgeSun>,
            Without<BridgeJoint>,
        ),
    >,
) {
    let Some(snapshot) = pending.0.take() else {
        return;
    };

    for (mut transform, mut projection, mut fog) in &mut cameras {
        *transform = GlobalTransform::from(camera_world_matrix(&snapshot.camera.view));

        if let Projection::Perspective(perspective) = projection.as_mut() {
            perspective.fov = snapshot.camera.fov_degrees.to_radians();
            perspective.near = snapshot.camera.near;
            perspective.far = snapshot.camera.far;
        }

        *fog = fog_component(&snapshot.fog);
    }

    for (mut transform, mut light) in &mut suns {
        let (color, strength) = unity_light_color(snapshot.sun_color);
        light.color = color;
        light.illuminance = strength * UNITY_DIRECTIONAL_TO_LUX;
        let direction = MIRROR_Z
            .transform_vector3(snapshot.sun_direction)
            .normalize_or_zero();

        if direction != Vec3::ZERO {
            *transform = GlobalTransform::from(Transform::from_rotation(Quat::from_rotation_arc(
                Vec3::NEG_Z,
                direction,
            )));
        }
    }

    ambient.color = Color::linear_rgb(snapshot.ambient.x, snapshot.ambient.y, snapshot.ambient.z);
    ambient.brightness = 1.0;

    sync_lights(&mut commands, &mut light_entities, &snapshot);
    sync_joints(&mut commands, &mut joint_sets, &mut joints, &snapshot);

    sync_renderers(
        &mut commands,
        &mut table,
        &tables,
        &placeholders,
        &joint_sets,
        &mut renderers,
        &snapshot,
    );
    current_frame.0 = snapshot.frame;
}

/// Spawns, updates and despawns renderer entities so they match the snapshot, one entity per submesh.
fn sync_renderers(
    commands: &mut Commands,
    table: &mut HandleTable,
    tables: &ResourceTables,
    placeholders: &Placeholders,
    joint_sets: &JointSets,
    renderers: &mut Query<
        (
            &mut GlobalTransform,
            &mut Visibility,
            &mut Mesh3d,
            &mut MeshMaterial3d<UrpLitMaterial>,
            &mut BridgeRenderer,
        ),
        (
            Without<BridgeCamera>,
            Without<BridgeSun>,
            Without<BridgeJoint>,
        ),
    >,
    snapshot: &Snapshot,
) {
    let mut seen: HashSet<u32> = HashSet::with_capacity(snapshot.renderers.len());

    for renderer in &snapshot.renderers {
        seen.insert(renderer.handle);
        let world = GlobalTransform::from(renderer_world_matrix(&renderer.world));
        let submeshes: Vec<Handle<Mesh>> = match tables.mesh(renderer.mesh) {
            Some(handles) if renderer.mesh != MESH_PLACEHOLDER_CUBE => handles.to_vec(),
            _ => vec![placeholders.cube.clone()],
        };

        // A skinned renderer binds its joints once; Bevy then skins in the vertex shader.
        let skin = renderer.skin.and_then(|(offset, count)| {
            let joints = joint_sets.0.get(&offset)?;
            let bindposes = tables.bindposes(renderer.mesh)?;
            (joints.len() == usize::try_from(count).ok()?).then(|| SkinnedMesh {
                inverse_bindposes: bindposes.clone(),
                joints: joints.clone(),
            })
        });
        let skin_key = skin.as_ref().and(renderer.skin);
        // A mesh with joint attributes selects Bevy's skinned pipeline, which panics in wgpu when the
        // entity has no skin bound (`skinned_mesh_layout` against `model_only_mesh_bind_group`), so
        // such a mesh stays hidden until its skeleton resolves.
        let drawable = skin.is_some() || tables.bindposes(renderer.mesh).is_none();
        let visibility = if renderer.visible && drawable {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };

        let entities = table.entities.entry(renderer.handle).or_default();

        for (index, mesh) in submeshes.iter().enumerate() {
            let material = renderer
                .materials
                .get(index)
                .or_else(|| renderer.materials.last())
                .copied()
                .filter(|&handle| handle != MATERIAL_PLACEHOLDER)
                .and_then(|handle| tables.material(handle).cloned())
                .unwrap_or_else(|| placeholders.material.clone());

            if let Some(entity) = entities.get(index).copied() {
                if let Ok((mut transform, mut current, mut mesh3d, mut material3d, mut marker)) =
                    renderers.get_mut(entity)
                {
                    *transform = world;
                    *current = visibility;

                    if mesh3d.0 != *mesh {
                        mesh3d.0 = mesh.clone();
                    }

                    if material3d.0 != material {
                        material3d.0 = material;
                    }

                    if marker.skin != skin_key {
                        marker.skin = skin_key;

                        match &skin {
                            Some(skinned) => {
                                commands.entity(entity).insert(skinned.clone());
                            }
                            None => {
                                commands.entity(entity).remove::<SkinnedMesh>();
                            }
                        }
                    }
                }
            } else {
                let mut spawned = commands.spawn((
                    BridgeRenderer {
                        handle: renderer.handle,
                        submesh: index,
                        skin: skin_key,
                    },
                    Mesh3d(mesh.clone()),
                    MeshMaterial3d(material),
                    world,
                    visibility,
                ));

                if let Some(skinned) = &skin {
                    spawned.insert(skinned.clone());
                }

                entities.push(spawned.id());
            }
        }

        while entities.len() > submeshes.len() {
            if let Some(extra) = entities.pop() {
                commands.entity(extra).despawn();
            }
        }
    }

    table.entities.retain(|handle, entities| {
        if seen.contains(handle) {
            return true;
        }

        for entity in entities.drain(..) {
            commands.entity(entity).despawn();
        }

        false
    });
}

/// Unity's light units are unitless multipliers of the albedo: a surface facing a light of intensity
/// `I` reflects `albedo * I`. Bevy's PBR reflects `albedo / pi * illuminance * exposure` for a
/// directional light and `albedo / pi * lumens / (4 pi) / d^2 * exposure` for a punctual one, so with
/// the exposure pinned to 1.0 these factors make both renderers agree on a white diffuse surface.
const UNITY_DIRECTIONAL_TO_LUX: f32 = core::f32::consts::PI;
const UNITY_PUNCTUAL_TO_LUMENS: f32 = 4.0 * core::f32::consts::PI * core::f32::consts::PI;
/// `Exposure` is `1 / (1.2 * 2^ev100)`; this ev100 makes it exactly 1.0 (`-log2(1.2)`).
const UNITY_EXPOSURE: Exposure = Exposure {
    ev100: -0.263_034_4,
};

/// Splits a Unity `color * intensity` into Bevy's normalised colour and a scalar strength.
fn unity_light_color(color: Vec3) -> (Color, f32) {
    let strength = color.max_element();

    if strength <= 0.0 {
        return (Color::WHITE, 0.0);
    }

    let normalised = color / strength;
    (
        Color::linear_rgb(normalised.x, normalised.y, normalised.z),
        strength,
    )
}

/// Keeps one joint entity per bone of every skeleton the snapshot references and writes their world
/// transforms; skeletons no renderer references any more are despawned.
fn sync_joints(
    commands: &mut Commands,
    joint_sets: &mut JointSets,
    joints: &mut Query<
        &mut GlobalTransform,
        (
            With<BridgeJoint>,
            Without<BridgeCamera>,
            Without<BridgeSun>,
            Without<BridgeRenderer>,
        ),
    >,
    snapshot: &Snapshot,
) {
    let mut seen: HashSet<u32> = HashSet::new();

    for (offset, count) in snapshot.renderers.iter().filter_map(|r| r.skin) {
        if !seen.insert(offset) {
            continue;
        }

        let Some(bones) = usize::try_from(offset)
            .ok()
            .zip(usize::try_from(count).ok())
            .and_then(|(start, len)| snapshot.bones.get(start..start.checked_add(len)?))
        else {
            continue;
        };

        let entities = joint_sets.0.entry(offset).or_default();

        if entities.len() != bones.len() {
            for entity in entities.drain(..) {
                commands.entity(entity).despawn();
            }
        }

        for (index, bone) in bones.iter().enumerate() {
            let transform = GlobalTransform::from(renderer_world_matrix(bone));

            match entities.get(index).copied() {
                Some(entity) => {
                    if let Ok(mut current) = joints.get_mut(entity) {
                        *current = transform;
                    }
                }
                None => entities.push(commands.spawn((BridgeJoint, transform)).id()),
            }
        }
    }

    joint_sets.0.retain(|offset, entities| {
        if seen.contains(offset) {
            return true;
        }

        for entity in entities.drain(..) {
            commands.entity(entity).despawn();
        }

        false
    });
}

fn sync_lights(commands: &mut Commands, light_entities: &mut LightEntities, snapshot: &Snapshot) {
    // Lights are few and change rarely; respawning them each frame keeps the logic trivial.
    for entity in light_entities.0.drain(..) {
        commands.entity(entity).despawn();
    }

    for (index, light) in snapshot.lights.iter().enumerate() {
        let (color, strength) = unity_light_color(light.color);
        let position = MIRROR_Z.transform_point3(light.position);
        let direction = MIRROR_Z
            .transform_vector3(light.direction)
            .normalize_or_zero();
        let rotation = if direction == Vec3::ZERO {
            Quat::IDENTITY
        } else {
            Quat::from_rotation_arc(Vec3::NEG_Z, direction)
        };
        let transform =
            GlobalTransform::from(Transform::from_translation(position).with_rotation(rotation));

        let entity = match light.kind {
            light_kind::POINT => commands
                .spawn((
                    BridgeLight { index },
                    PointLight {
                        color,
                        intensity: strength * UNITY_PUNCTUAL_TO_LUMENS,
                        range: light.range,
                        shadows_enabled: light.shadows,
                        ..default()
                    },
                    transform,
                ))
                .id(),
            light_kind::SPOT => commands
                .spawn((
                    BridgeLight { index },
                    SpotLight {
                        color,
                        intensity: strength * UNITY_PUNCTUAL_TO_LUMENS,
                        range: light.range,
                        outer_angle: (light.spot_angle_degrees * 0.5).to_radians(),
                        inner_angle: (light.spot_angle_degrees * 0.4).to_radians(),
                        shadows_enabled: light.shadows,
                        ..default()
                    },
                    transform,
                ))
                .id(),
            _ => commands
                .spawn((
                    BridgeLight { index },
                    DirectionalLight {
                        color,
                        illuminance: strength * UNITY_DIRECTIONAL_TO_LUX,
                        shadows_enabled: light.shadows,
                        ..default()
                    },
                    transform,
                ))
                .id(),
        };

        light_entities.0.push(entity);
    }
}

fn fog_component(fog: &snapshot::SnapshotFog) -> DistanceFog {
    let color = Color::linear_rgb(fog.color.x, fog.color.y, fog.color.z);

    let falloff = match fog.mode {
        fog_mode::LINEAR => FogFalloff::Linear {
            start: fog.start,
            end: fog.end,
        },
        fog_mode::EXPONENTIAL => FogFalloff::Exponential {
            density: fog.density,
        },
        fog_mode::EXPONENTIAL_SQUARED => FogFalloff::ExponentialSquared {
            density: fog.density,
        },
        _ => FogFalloff::Linear {
            start: f32::MAX / 4.0,
            end: f32::MAX / 2.0,
        },
    };

    DistanceFog {
        color,
        falloff,
        ..default()
    }
}

/// Points the camera at the output view of this frame's buffer.
fn select_output_view(
    frame: Res<CurrentFrame>,
    views: Option<Res<OutputViews>>,
    mut cameras: Query<&mut Camera, With<BridgeCamera>>,
) {
    let Some(views) = views else {
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
