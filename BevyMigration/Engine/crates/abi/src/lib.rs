//! The `extern "C"` surface shared by Unity (through csbindgen bindings) and the Rust side
//! (BevyMigration/docs/README.md § 7 step 2, § 8). Only `#[repr(C)]` data lives here; the entry points
//! themselves are exported by `crates/unity_bridge`, which implements them over these types.
//!
//! Every struct here is mirrored byte for byte on the C# side, so a change is an ABI change
//! (bump [`ABI_VERSION`]) and ships with both sides in the same PR (plan § 5).

#![no_std]

use core::ffi::{c_char, c_void};

/// Bumped on any layout or signature change; `bm_init` refuses a mismatching caller.
pub const ABI_VERSION: u32 = 1;

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

/// Mesh handle value meaning "the bridge's built-in unit cube" until resource upload lands in step 3.
pub const MESH_PLACEHOLDER_CUBE: u32 = 0;

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
    pub renderer_count: u32,
    /// `renderer_count` generational handles (plan § 16 "Handle").
    pub handles: *const u32,
    /// `renderer_count` local-to-world matrices.
    pub worlds: *const BmMat4,
    /// `renderer_count` mesh handles ([`MESH_PLACEHOLDER_CUBE`] in step 2).
    pub meshes: *const u32,
    /// `renderer_count` [`renderer_flags`].
    pub flags: *const u16,
}

/// Kinds of [`BmResourceCmd`]; the first real kinds land with mesh and texture upload in step 3.
#[repr(u32)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BmResourceCmdKind {
    None = 0,
}

/// A resource command: `kind` decides how `data` is read; `handle` is allocated by the host.
#[repr(C)]
#[derive(Debug, Clone, Copy)]
pub struct BmResourceCmd {
    pub kind: BmResourceCmdKind,
    pub handle: u32,
    pub data: *const u8,
    pub len: usize,
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
    }
}
