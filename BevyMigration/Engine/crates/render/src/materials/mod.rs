//! Material families (plan § 8): one `MaterialExtension` per family with its WGSL beside it.

use bevy::{
    asset::{load_internal_asset, weak_handle},
    pbr::{ExtendedMaterial, MaterialExtension, MaterialPlugin},
    prelude::*,
    render::render_resource::{AsBindGroup, ShaderRef},
};

/// Bevy's PBR with Unity's map conventions and the scene-bounds dithering.
pub type UrpLitMaterial = ExtendedMaterial<UrpLitExtension>;

pub const URP_LIT_SHADER_HANDLE: Handle<Shader> =
    weak_handle!("5b5a0b35-1b16-4a3f-b4c1-9a5a3f8c2e01");

/// Bit flags of [`UrpLitParams::flags`]; mirrored in `urp_lit.wgsl`.
pub mod urp_lit_flags {
    pub const METALLIC_GLOSS_MAP: u32 = 1;
    pub const OCCLUSION_MAP: u32 = 2;
    pub const SCENE_BOUNDS: u32 = 4;
}

/// Bevy's PBR with Unity's map conventions and the scene-bounds dithering. The uniform fields share
/// binding 100 and are packed, in this order, into the `UrpLitParams` struct `urp_lit.wgsl` declares.
#[derive(Asset, AsBindGroup, Reflect, Debug, Clone)]
#[reflect(Default)]
pub struct UrpLitExtension {
    #[uniform(100)]
    pub metallic: f32,
    #[uniform(100)]
    pub smoothness: f32,
    #[uniform(100)]
    pub occlusion_strength: f32,
    #[uniform(100)]
    pub flags: u32,
    #[uniform(100)]
    pub bounds_min: Vec2,
    #[uniform(100)]
    pub bounds_max: Vec2,
    #[texture(101)]
    #[sampler(102)]
    pub metallic_gloss_map: Option<Handle<Image>>,
    #[texture(103)]
    #[sampler(104)]
    pub occlusion_map: Option<Handle<Image>>,
}

impl Default for UrpLitExtension {
    fn default() -> Self {
        Self {
            metallic: 0.0,
            smoothness: 0.5,
            occlusion_strength: 1.0,
            flags: 0,
            bounds_min: Vec2::ZERO,
            bounds_max: Vec2::ZERO,
            metallic_gloss_map: None,
            occlusion_map: None,
        }
    }
}

impl MaterialExtension for UrpLitExtension {
    type Base = StandardMaterial;

    fn fragment_shader() -> ShaderRef {
        URP_LIT_SHADER_HANDLE.into()
    }
}

#[derive(Debug, Default)]
pub struct MaterialFamiliesPlugin;

impl Plugin for MaterialFamiliesPlugin {
    fn build(&self, app: &mut App) {
        load_internal_asset!(
            app,
            URP_LIT_SHADER_HANDLE,
            "urp_lit.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins(MaterialPlugin::<UrpLitMaterial>::default());
    }
}
