//! The `extern "C"` surface shared by Unity (through csbindgen bindings) and the Rust side
//! (BevyMigration/docs/README.md § 7 step 2, § 8). Only `#[repr(C)]` data lives here; the entry points
//! themselves are exported by `crates/unity_bridge`, which implements them over these types.
//!
//! Every struct here is mirrored byte for byte on the C# side, so a change is an ABI change
//! (bump [`ABI_VERSION`]) and ships with both sides in the same PR (plan § 5).

#![no_std]

use core::ffi::{c_char, c_void};

/// Bumped on any layout or signature change; `bm_init` refuses a mismatching caller.
pub const ABI_VERSION: u32 = 3;

/// Number of output buffers the bridge alternates between: Bevy renders frame N into one while Unity
/// composites frame N-1 from the other (plan § 8).
pub const OUTPUT_BUFFER_COUNT: u32 = 2;

/// Result of every entry point. Panics never cross the boundary: they become [`BmResult::Panicked`]
/// plus a log line.
#[repr(i32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BmResult {
    Ok = 0,
    NotInitialized = 1,
    AlreadyInitialized = 2,
    InvalidArgument = 3,
    AbiMismatch = 4,
    Unsupported = 5,
    Panicked = 6,
    Internal = 7,
}

/// Severity of a line handed to the host's log callback; matches Unity's `LogType` ordering loosely.
#[repr(i32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BmLogLevel {
    Error = 0,
    Warning = 2,
    Info = 3,
}

/// Receives log lines from the bridge. The message is valid only for the duration of the call.
pub type BmLogCallback = Option<unsafe extern "C" fn(level: BmLogLevel, message: *const c_char)>;

/// What `bm_init` needs from the host.
#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct BmInitConfig {
    /// Must equal [`ABI_VERSION`].
    pub abi_version: u32,
    /// Size of the output buffers in pixels.
    pub width: u32,
    pub height: u32,
    /// How the rendered frames reach the host.
    pub output: BmOutputKind,
    pub log: BmLogCallback,
}

/// How the render output is shared with the host.
#[repr(i32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BmOutputKind {
    /// Rendered into textures the host cannot see; readable through `bm_read_output` (tests, tooling).
    Offscreen = 0,
    /// macOS: `IOSurface`-backed textures both Metal devices wrap (plan § 8 "Output sharing").
    IoSurface = 1,
}

/// Column-major 4x4 matrix, the layout of both `UnityEngine.Matrix4x4` and `glam::Mat4`.
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct BmMat4 {
    pub cols: [f32; 16],
}

impl BmMat4 {
    pub const IDENTITY: Self = Self {
        cols: [
            1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0,
        ],
    };
}

/// The camera as Unity computed it; Bevy renders with these matrices verbatim (plan § 8 snapshot schema).
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct BmCamera {
    /// World to view.
    pub view: BmMat4,
    /// View to clip, Unity convention (`Camera.projectionMatrix`, OpenGL-style clip space).
    pub projection: BmMat4,
    pub near: f32,
    pub far: f32,
    /// Vertical field of view in degrees; zero for an orthographic camera.
    pub fov_degrees: f32,
}

/// Renderer flags carried in [`BmSnapshot::flags`].
pub mod renderer_flags {
    pub const VISIBLE: u16 = 1;
    pub const CAST_SHADOWS: u16 = 2;
    pub const RECEIVE_SHADOWS: u16 = 4;
}

/// Mesh handle value meaning "the bridge's built-in unit cube"; uploaded meshes use handles from 1.
pub const MESH_PLACEHOLDER_CUBE: u32 = 0;

/// Material handle value meaning "the bridge's placeholder material"; uploaded materials use handles from 1.
pub const MATERIAL_PLACEHOLDER: u32 = 0;

/// Texture handle value meaning "no texture"; uploaded textures use handles from 1.
pub const TEXTURE_NONE: u32 = 0;

/// Light kinds in [`BmLight::kind`].
pub mod light_kind {
    pub const DIRECTIONAL: u32 = 0;
    pub const POINT: u32 = 1;
    pub const SPOT: u32 = 2;
}

/// One light of the snapshot (plan § 8 "Lights").
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct BmLight {
    pub kind: u32,
    /// Non-zero when the light casts shadows.
    pub shadows: u32,
    /// World position (point, spot).
    pub position: [f32; 3],
    /// Direction the light travels, world space (directional, spot).
    pub direction: [f32; 3],
    /// Linear RGB already multiplied by intensity.
    pub color: [f32; 3],
    /// Metres (point, spot).
    pub range: f32,
    /// Full cone angle in degrees (spot).
    pub spot_angle: f32,
}

/// Fog modes in [`BmFog::mode`], Unity's `FogMode` values.
pub mod fog_mode {
    pub const NONE: u32 = 0;
    pub const LINEAR: u32 = 1;
    pub const EXPONENTIAL: u32 = 2;
    pub const EXPONENTIAL_SQUARED: u32 = 3;
}

/// Scene fog as Unity's `RenderSettings` hold it.
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct BmFog {
    pub mode: u32,
    pub color: [f32; 3],
    pub density: f32,
    pub start: f32,
    pub end: f32,
}

/// One frame of renderer state, structure of arrays, pointed into the host's double buffer. The bridge
/// copies everything out during `bm_submit_snapshot`; nothing is retained past the call (plan § 9 threading).
#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct BmSnapshot {
    pub frame: u64,
    pub camera: BmCamera,
    /// Linear ambient colour, RGB.
    pub ambient: [f32; 3],
    /// Directional light: direction the light travels, world space, and linear RGB intensity.
    pub sun_direction: [f32; 3],
    pub sun_color: [f32; 3],
    pub fog: BmFog,
    pub renderer_count: u32,
    /// `renderer_count` generational handles (plan § 16 "Handle").
    pub handles: *const u32,
    /// `renderer_count` local-to-world matrices.
    pub worlds: *const BmMat4,
    /// `renderer_count` mesh handles ([`MESH_PLACEHOLDER_CUBE`] for the built-in cube).
    pub meshes: *const u32,
    /// `renderer_count` [`renderer_flags`].
    pub flags: *const u16,
    /// `renderer_count` offsets into `material_handles`.
    pub material_first: *const u32,
    /// `renderer_count` counts of materials (one per submesh) starting at `material_first`.
    pub material_count: *const u8,
    /// `material_handle_count` material handles, [`MATERIAL_PLACEHOLDER`] where none was uploaded.
    pub material_handle_count: u32,
    pub material_handles: *const u32,
    /// `light_count` lights.
    pub light_count: u32,
    pub lights: *const BmLight,
    /// `skinned_count` skinned renderers and the `bone_count` world matrices they index.
    pub skinned_count: u32,
    pub skinned: *const BmSkinned,
    pub bone_count: u32,
    pub bones: *const BmMat4,
}

/// Kinds of [`BmResourceCmd`] (plan § 8 "Resource transfer", phase 1: copy once per asset).
#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BmResourceCmdKind {
    None = 0,
    /// `data` is a [`BmMeshHeader`] followed by the streams it describes.
    MeshUpload = 1,
    MeshRelease = 2,
    /// `data` is a [`BmTextureHeader`] followed by every mip level, tightly packed, largest first.
    TextureUpload = 3,
    TextureRelease = 4,
    /// `data` is one [`BmMaterial`].
    MaterialUpload = 5,
    MaterialRelease = 6,
}

/// A resource command: `kind` decides how `data` is read; `handle` is allocated by the host and must
/// be non-zero. The bridge copies the payload before returning.
#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct BmResourceCmd {
    pub kind: BmResourceCmdKind,
    pub handle: u32,
    pub data: *const u8,
    pub len: usize,
}

/// Which optional vertex streams a mesh upload carries ([`BmMeshHeader::attributes`]); positions are
/// always present. Streams follow the header in this order, each tightly packed per vertex.
pub mod mesh_attributes {
    /// `[f32; 3]` per vertex.
    pub const NORMAL: u32 = 1;
    /// `[f32; 4]` per vertex, w is the bitangent sign.
    pub const TANGENT: u32 = 2;
    /// `[f32; 2]` per vertex.
    pub const UV0: u32 = 4;
    /// `[f32; 2]` per vertex.
    pub const UV1: u32 = 8;
    /// `[f32; 4]` per vertex, linear RGBA.
    pub const COLOR: u32 = 16;
    /// `[u16; 4]` joint indices then `[f32; 4]` joint weights per vertex; `bindpose_count` inverse
    /// bind poses follow the submeshes.
    pub const BONES: u32 = 32;
}

/// Index formats in [`BmMeshHeader::index_format`].
pub mod index_format {
    pub const U16: u32 = 0;
    pub const U32: u32 = 1;
}

/// Mesh upload payload header. After the header: positions (`[f32; 3]` x `vertex_count`), the optional
/// streams in [`mesh_attributes`] order, `index_count` indices in `index_format`, then `submesh_count`
/// [`BmSubmesh`]. Vertices are in Unity's left-handed mesh space with Unity's clockwise winding; the
/// bridge mirrors and rewinds them.
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct BmMeshHeader {
    pub vertex_count: u32,
    pub index_count: u32,
    pub submesh_count: u32,
    pub attributes: u32,
    pub index_format: u32,
    /// Inverse bind poses (`BmMat4`, one per joint of the skeleton the mesh is skinned to) after the
    /// submeshes; 0 unless [`mesh_attributes::BONES`] is set.
    pub bindpose_count: u32,
}

/// A skinned renderer of the snapshot: its joints are `bone_count` world matrices starting at
/// `bone_offset` in [`BmSnapshot::bones`], in the order the mesh's joint indices and bind poses use.
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct BmSkinned {
    pub renderer_index: u32,
    pub bone_offset: u32,
    pub bone_count: u32,
}

/// A triangle-list submesh: a range of indices.
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct BmSubmesh {
    pub index_start: u32,
    pub index_count: u32,
}

/// Texture formats in [`BmTextureHeader::format`]: the block-compressed formats Unity's asset bundles
/// ship, plus uncompressed fallbacks.
#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BmTextureFormat {
    Rgba8 = 0,
    Bgra8 = 1,
    /// DXT1.
    Bc1 = 2,
    /// DXT5.
    Bc3 = 3,
    Bc4 = 4,
    Bc5 = 5,
    Bc7 = 6,
    R8 = 7,
    Rg8 = 8,
}

/// Sampler wrap modes in [`BmTextureHeader::wrap_u`] and `wrap_v`.
pub mod wrap_mode {
    pub const REPEAT: u32 = 0;
    pub const CLAMP: u32 = 1;
    pub const MIRROR: u32 = 2;
}

/// Sampler filters in [`BmTextureHeader::filter`].
pub mod filter_mode {
    pub const POINT: u32 = 0;
    pub const BILINEAR: u32 = 1;
    pub const TRILINEAR: u32 = 2;
}

/// Texture upload payload header. Rows follow Unity's order (first row at the bottom); the bridge
/// uploads them as they are and Unity's UVs sample them consistently.
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct BmTextureHeader {
    pub width: u32,
    pub height: u32,
    pub mip_count: u32,
    pub format: BmTextureFormat,
    /// Non-zero when the data is sRGB encoded (colour textures), zero for linear data (normals, masks).
    pub srgb: u32,
    pub wrap_u: u32,
    pub wrap_v: u32,
    pub filter: u32,
}

/// Material families (plan § 8 "Material families"); the inventory's `UrpLit` and `Unlit` arrive first.
#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BmMaterialFamily {
    UrpLit = 0,
    Unlit = 1,
    /// `TextMeshPro` world text: unlit, the base map's alpha is the signed distance field.
    TmpSdf = 2,
}

/// Alpha modes of URP Lit.
#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BmAlphaMode {
    Opaque = 0,
    Cutout = 1,
    Fade = 2,
    Transparent = 3,
}

/// The URP Lit property blob, with the `SceneBounds` extension that dithers geometry outside the
/// parcel (plan § 7, step 3). Texture fields hold texture handles or [`TEXTURE_NONE`].
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct BmMaterial {
    pub family: BmMaterialFamily,
    pub alpha_mode: BmAlphaMode,
    pub double_sided: u32,
    /// Non-zero when `scene_bounds_min`/`max` apply.
    pub scene_bounds_enabled: u32,
    /// Linear RGBA.
    pub base_color: [f32; 4],
    pub metallic: f32,
    pub smoothness: f32,
    pub alpha_cutoff: f32,
    pub normal_scale: f32,
    pub occlusion_strength: f32,
    /// Linear RGB.
    pub emission: [f32; 3],
    pub base_map: u32,
    pub normal_map: u32,
    /// Unity's `_MetallicGlossMap`: metallic in R, smoothness in A.
    pub metallic_gloss_map: u32,
    /// Unity's `_OcclusionMap`: occlusion in G.
    pub occlusion_map: u32,
    pub emission_map: u32,
    /// `_BaseMap_ST`: scale xy, offset zw.
    pub base_map_st: [f32; 4],
    /// World-space XZ rectangle of the scene's parcels.
    pub scene_bounds_min: [f32; 2],
    pub scene_bounds_max: [f32; 2],
}

/// One shared output buffer as the host sees it.
#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct BmOutputTexture {
    pub width: u32,
    pub height: u32,
    /// macOS: `id<MTLTexture>` created on the host's Metal device for `Texture2D.CreateExternalTexture`;
    /// null for [`BmOutputKind::Offscreen`].
    pub host_texture: *mut c_void,
    /// macOS: the `IOSurfaceRef` behind it; null for [`BmOutputKind::Offscreen`].
    pub io_surface: *mut c_void,
}

/// Which output buffer the host may composite this frame.
#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct BmFrameStatus {
    /// Snapshot frame number whose rendering has completed on the GPU; 0 before the first frame.
    pub completed_frame: u64,
    /// Output buffer holding `completed_frame`; always below [`OUTPUT_BUFFER_COUNT`].
    pub completed_buffer: u32,
    /// Frames submitted but not yet completed.
    pub in_flight: u32,
}

/// Unity render-thread event ids passed to `bm_render_event` (plan § 7 step 2) through `GL.IssuePluginEvent`.
pub mod render_events {
    /// Fired by the composite feature right before it samples the completed buffer.
    pub const BEFORE_COMPOSITE: i32 = 1;
}

#[cfg(test)]
mod tests {
    use super::*;
    use core::mem::{align_of, size_of};

    #[test]
    fn layouts_are_what_the_csharp_mirror_assumes() {
        assert_eq!(size_of::<BmMat4>(), 64);
        assert_eq!(size_of::<BmCamera>(), 64 * 2 + 4 * 3);
        assert_eq!(size_of::<BmResult>(), 4);
        assert_eq!(size_of::<BmLogLevel>(), 4);
        assert_eq!(size_of::<BmOutputKind>(), 4);
        assert_eq!(align_of::<BmSnapshot>(), 8);
        assert_eq!(size_of::<BmFrameStatus>(), 16);
        assert_eq!(size_of::<BmInitConfig>(), 4 * 4 + size_of::<usize>());
        assert_eq!(size_of::<BmLight>(), 4 * 2 + 4 * 9 + 4 * 2);
        assert_eq!(size_of::<BmFog>(), 4 * 7);
        assert_eq!(size_of::<BmMeshHeader>(), 4 * 6);
        assert_eq!(size_of::<BmSkinned>(), 4 * 3);
        assert_eq!(size_of::<BmSubmesh>(), 8);
        assert_eq!(size_of::<BmTextureHeader>(), 4 * 8);
        assert_eq!(
            size_of::<BmMaterial>(),
            4 * 4 + 4 * 4 + 4 * 5 + 4 * 3 + 4 * 5 + 4 * 4 + 4 * 4
        );
    }
}
