// The workspace denies `unsafe`; calling the extern "C" surface is what this test is for.
#![allow(unsafe_code)]
//! The whole ABI path without Unity: init offscreen, submit a snapshot with one cube in front of
//! the camera, wait for the frame, read the pixels back. Needs a GPU; the Metal path is what the
//! macOS development machines have.

use abi::{
    BmAlphaMode, BmCamera, BmFog, BmFrameStatus, BmInitConfig, BmMat4, BmMaterial,
    BmMaterialFamily, BmOutputKind, BmOutputTexture, BmResourceCmd, BmResourceCmdKind, BmResult,
    BmSnapshot, MESH_PLACEHOLDER_CUBE, TEXTURE_NONE, fog_mode, index_format, mesh_attributes,
    renderer_flags,
};
use parking_lot::Mutex;
use std::time::{Duration, Instant};

/// The bridge is process-global; the tests take turns.
static BRIDGE_LOCK: Mutex<()> = Mutex::new(());

const WIDTH: u32 = 96;
const HEIGHT: u32 = 64;

fn config(kind: BmOutputKind) -> BmInitConfig {
    BmInitConfig {
        abi_version: abi::ABI_VERSION,
        width: WIDTH,
        height: HEIGHT,
        output: kind,
        log: None,
    }
}

/// Unity-style camera at the origin looking down +Z, a cube 4 m ahead.
fn snapshot(
    frame: u64,
    handles: &[u32],
    worlds: &[BmMat4],
    meshes: &[u32],
    flags: &[u16],
) -> BmSnapshot {
    // worldToCameraMatrix for a camera at the origin looking +Z: OpenGL-style view, so Z is negated.
    let view = BmMat4 {
        cols: [
            1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, -1.0, 0.0, 0.0, 0.0, 0.0, 1.0,
        ],
    };

    BmSnapshot {
        frame,
        camera: BmCamera {
            view,
            projection: BmMat4::IDENTITY,
            near: 0.1,
            far: 100.0,
            fov_degrees: 60.0,
        },
        ambient: [0.3, 0.3, 0.3],
        sun_direction: [0.2, -1.0, 0.5],
        sun_color: [1.0, 1.0, 1.0],
        fog: BmFog {
            mode: fog_mode::NONE,
            color: [0.0; 3],
            density: 0.0,
            start: 0.0,
            end: 0.0,
        },
        renderer_count: u32::try_from(handles.len()).unwrap_or(0),
        handles: handles.as_ptr(),
        worlds: worlds.as_ptr(),
        meshes: meshes.as_ptr(),
        flags: flags.as_ptr(),
        material_first: std::ptr::null(),
        material_count: std::ptr::null(),
        material_handle_count: 0,
        material_handles: std::ptr::null(),
        light_count: 0,
        lights: std::ptr::null(),
        skinned_count: 0,
        skinned: std::ptr::null(),
        bone_count: 0,
        bones: std::ptr::null(),
    }
}

/// A snapshot whose single renderer uses mesh `mesh` and material `material` (handles from uploads).
fn snapshot_with_material(
    frame: u64,
    handles: &[u32],
    worlds: &[BmMat4],
    meshes: &[u32],
    flags: &[u16],
    material_first: &[u32],
    material_count: &[u8],
    material_handles: &[u32],
) -> BmSnapshot {
    let mut snapshot = snapshot(frame, handles, worlds, meshes, flags);
    snapshot.material_first = material_first.as_ptr();
    snapshot.material_count = material_count.as_ptr();
    snapshot.material_handle_count = u32::try_from(material_handles.len()).unwrap_or(0);
    snapshot.material_handles = material_handles.as_ptr();
    snapshot
}

fn push_u32(out: &mut Vec<u8>, value: u32) {
    out.extend_from_slice(&value.to_le_bytes());
}

fn push_f32s(out: &mut Vec<u8>, values: &[f32]) {
    for value in values {
        out.extend_from_slice(&value.to_le_bytes());
    }
}

/// A 2x2 m quad facing Unity's -Z (towards a camera looking down +Z), clockwise as Unity winds it.
fn quad_payload() -> Vec<u8> {
    let mut out = Vec::new();
    push_u32(&mut out, 4);
    push_u32(&mut out, 6);
    push_u32(&mut out, 1);
    push_u32(&mut out, mesh_attributes::NORMAL | mesh_attributes::UV0);
    push_u32(&mut out, index_format::U16);
    push_u32(&mut out, 0);
    push_f32s(
        &mut out,
        &[
            -1.0, -1.0, 0.0, -1.0, 1.0, 0.0, 1.0, 1.0, 0.0, 1.0, -1.0, 0.0,
        ],
    );
    push_f32s(
        &mut out,
        &[
            0.0, 0.0, -1.0, 0.0, 0.0, -1.0, 0.0, 0.0, -1.0, 0.0, 0.0, -1.0,
        ],
    );
    push_f32s(&mut out, &[0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 0.0]);
    for index in [0u16, 1, 2, 0, 2, 3] {
        out.extend_from_slice(&index.to_le_bytes());
    }
    push_u32(&mut out, 0);
    push_u32(&mut out, 6);
    out
}

fn green_unlit_material() -> BmMaterial {
    BmMaterial {
        family: BmMaterialFamily::Unlit,
        alpha_mode: BmAlphaMode::Opaque,
        double_sided: 0,
        scene_bounds_enabled: 0,
        base_color: [0.0, 1.0, 0.0, 1.0],
        metallic: 0.0,
        smoothness: 0.5,
        alpha_cutoff: 0.5,
        normal_scale: 1.0,
        occlusion_strength: 1.0,
        emission: [0.0; 3],
        base_map: TEXTURE_NONE,
        normal_map: TEXTURE_NONE,
        metallic_gloss_map: TEXTURE_NONE,
        occlusion_map: TEXTURE_NONE,
        emission_map: TEXTURE_NONE,
        base_map_st: [1.0, 1.0, 0.0, 0.0],
        scene_bounds_min: [0.0; 2],
        scene_bounds_max: [0.0; 2],
    }
}

fn cube_at(z: f32) -> BmMat4 {
    let mut m = BmMat4::IDENTITY;
    m.cols[14] = z;
    m
}

fn wait_for_frame(frame: u64) -> BmFrameStatus {
    let deadline = Instant::now() + Duration::from_mins(1);
    let mut status = BmFrameStatus {
        completed_frame: 0,
        completed_buffer: 0,
        in_flight: 0,
    };

    loop {
        // SAFETY: a valid out pointer.
        assert_eq!(
            unsafe { unity_bridge::bm_begin_frame(std::ptr::from_mut(&mut status)) },
            BmResult::Ok
        );

        if status.completed_frame >= frame {
            return status;
        }

        assert!(
            Instant::now() < deadline,
            "frame {frame} never completed; last status {status:?}"
        );
        std::thread::sleep(Duration::from_millis(5));
    }
}

fn read_pixels() -> Vec<u8> {
    let mut needed = 0usize;
    // SAFETY: a null buffer with a valid `written` pointer asks for the size.
    assert_eq!(
        unsafe {
            unity_bridge::bm_read_output(std::ptr::null_mut(), 0, std::ptr::from_mut(&mut needed))
        },
        BmResult::InvalidArgument
    );
    assert_eq!(needed, (WIDTH * HEIGHT * 4) as usize);
    let mut pixels = vec![0u8; needed];
    // SAFETY: the buffer is `needed` bytes long.
    assert_eq!(
        unsafe {
            unity_bridge::bm_read_output(
                pixels.as_mut_ptr(),
                pixels.len(),
                std::ptr::from_mut(&mut needed),
            )
        },
        BmResult::Ok
    );
    pixels
}

fn pixel(pixels: &[u8], x: u32, y: u32) -> [u8; 4] {
    let i = ((y * WIDTH + x) * 4) as usize;
    [pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]]
}

#[test]
#[cfg_attr(
    not(target_os = "macos"),
    ignore = "needs a GPU; exercised on the macOS development machines"
)]
fn renders_a_cube_offscreen_and_reads_it_back() {
    let _guard = BRIDGE_LOCK.lock();
    assert_eq!(unity_bridge::bm_abi_version(), abi::ABI_VERSION);

    // SAFETY: valid config pointer.
    assert_eq!(
        unsafe { unity_bridge::bm_init(std::ptr::from_ref(&config(BmOutputKind::Offscreen))) },
        BmResult::Ok
    );

    let handles = [7u32];
    let worlds = [cube_at(4.0)];
    let meshes = [MESH_PLACEHOLDER_CUBE];
    let flags = [renderer_flags::VISIBLE];

    for frame in 1..=3u64 {
        let snapshot = snapshot(frame, &handles, &worlds, &meshes, &flags);
        // SAFETY: the arrays hold renderer_count elements.
        assert_eq!(
            unsafe { unity_bridge::bm_submit_snapshot(std::ptr::from_ref(&snapshot)) },
            BmResult::Ok
        );
        let status = wait_for_frame(frame);
        assert_eq!(
            status.completed_buffer,
            u32::try_from(frame % 2).unwrap_or(0)
        );
    }

    let pixels = read_pixels();
    let center = pixel(&pixels, WIDTH / 2, HEIGHT / 2);
    let corner = pixel(&pixels, 1, 1);

    assert_eq!(center[3], 255, "opaque output expected, got {center:?}");
    assert_ne!(
        center, corner,
        "the cube in the middle must differ from the clear colour in the corner"
    );

    // The placeholder material is orange: more red than blue in the centre, unlike the transparent clear colour.
    // BGRA: index 2 is red, index 0 is blue.
    assert!(
        center[2] > center[0],
        "centre pixel {center:?} (BGRA) should be reddish"
    );
    assert_eq!(
        corner[3],
        0,
        "corner pixel {corner:?} (BGRA) should be the transparent clear colour Unity composites over"
    );

    // An empty snapshot removes the cube: the centre becomes the clear colour again.
    let empty = snapshot(4, &[], &[], &[], &[]);
    // SAFETY: zero renderers, pointers unused.
    assert_eq!(
        unsafe { unity_bridge::bm_submit_snapshot(std::ptr::from_ref(&empty)) },
        BmResult::Ok
    );
    wait_for_frame(4);
    let pixels = read_pixels();
    assert_eq!(pixel(&pixels, WIDTH / 2, HEIGHT / 2), pixel(&pixels, 1, 1));

    assert_eq!(unity_bridge::bm_shutdown(), BmResult::Ok);
    assert_eq!(
        unity_bridge::bm_shutdown(),
        BmResult::Ok,
        "shutdown is idempotent"
    );
}

#[test]
#[cfg_attr(
    not(target_os = "macos"),
    ignore = "needs a GPU; exercised on the macOS development machines"
)]
fn renders_an_uploaded_quad_with_an_uploaded_material() {
    let _guard = BRIDGE_LOCK.lock();
    // SAFETY: valid config pointer.
    assert_eq!(
        unsafe { unity_bridge::bm_init(std::ptr::from_ref(&config(BmOutputKind::Offscreen))) },
        BmResult::Ok
    );

    let payload = quad_payload();
    let mesh_cmd = BmResourceCmd {
        kind: BmResourceCmdKind::MeshUpload,
        handle: 1,
        data: payload.as_ptr(),
        len: payload.len(),
    };
    // SAFETY: `data` holds `len` readable bytes.
    assert_eq!(
        unsafe { unity_bridge::bm_resource_cmd(std::ptr::from_ref(&mesh_cmd)) },
        BmResult::Ok
    );

    let material = green_unlit_material();
    // SAFETY: a valid material pointer.
    assert_eq!(
        unsafe { unity_bridge::bm_upload_material(1, std::ptr::from_ref(&material)) },
        BmResult::Ok
    );

    let handles = [5u32];
    let worlds = [cube_at(4.0)];
    let meshes = [1u32];
    let flags = [renderer_flags::VISIBLE];
    let material_first = [0u32];
    let material_count = [1u8];
    let material_handles = [1u32];

    for frame in 1..=2u64 {
        let snapshot = snapshot_with_material(
            frame,
            &handles,
            &worlds,
            &meshes,
            &flags,
            &material_first,
            &material_count,
            &material_handles,
        );
        // SAFETY: the arrays hold renderer_count elements and one material handle.
        assert_eq!(
            unsafe { unity_bridge::bm_submit_snapshot(std::ptr::from_ref(&snapshot)) },
            BmResult::Ok
        );
        wait_for_frame(frame);
    }

    let pixels = read_pixels();
    let center = pixel(&pixels, WIDTH / 2, HEIGHT / 2);
    let corner = pixel(&pixels, 1, 1);
    // BGRA: green is index 1; the unlit quad must be green in front of the transparent clear colour.
    assert!(
        center[1] > 150 && center[1] > center[0] * 2 && center[1] > center[2] * 2,
        "centre pixel {center:?} (BGRA) should be green"
    );
    assert_ne!(center, corner);

    let bad = BmResourceCmd {
        kind: BmResourceCmdKind::MeshUpload,
        handle: 0,
        data: payload.as_ptr(),
        len: payload.len(),
    };
    // SAFETY: valid pointer; handle 0 is reserved and must be refused.
    assert_eq!(
        unsafe { unity_bridge::bm_resource_cmd(std::ptr::from_ref(&bad)) },
        BmResult::InvalidArgument
    );

    assert_eq!(unity_bridge::bm_shutdown(), BmResult::Ok);
}

#[test]
#[cfg(target_os = "macos")]
fn renders_into_an_iosurface_readable_from_the_cpu() {
    let _guard = BRIDGE_LOCK.lock();

    // No Unity device in a test process: the bridge keeps the Bevy-side surface and the host reads it.
    // SAFETY: valid config pointer.
    assert_eq!(
        unsafe { unity_bridge::bm_init(std::ptr::from_ref(&config(BmOutputKind::IoSurface))) },
        BmResult::Ok
    );

    let mut output = BmOutputTexture {
        width: 0,
        height: 0,
        host_texture: std::ptr::null_mut(),
        io_surface: std::ptr::null_mut(),
    };
    // SAFETY: valid out pointer.
    assert_eq!(
        unsafe { unity_bridge::bm_get_output(1, std::ptr::from_mut(&mut output)) },
        BmResult::Ok
    );
    assert_eq!((output.width, output.height), (WIDTH, HEIGHT));
    assert!(
        !output.io_surface.is_null(),
        "IOSurface outputs expose the surface"
    );
    assert!(
        output.host_texture.is_null(),
        "no Unity device, so no host texture"
    );

    let handles = [7u32];
    let worlds = [cube_at(4.0)];
    let meshes = [MESH_PLACEHOLDER_CUBE];
    let flags = [renderer_flags::VISIBLE];

    for frame in 1..=2u64 {
        let snapshot = snapshot(frame, &handles, &worlds, &meshes, &flags);
        // SAFETY: the arrays hold renderer_count elements.
        assert_eq!(
            unsafe { unity_bridge::bm_submit_snapshot(std::ptr::from_ref(&snapshot)) },
            BmResult::Ok
        );
        wait_for_frame(frame);
    }

    let pixels = read_pixels();
    let center = pixel(&pixels, WIDTH / 2, HEIGHT / 2);
    let corner = pixel(&pixels, 1, 1);
    assert_eq!(center[3], 255, "opaque output expected, got {center:?}");
    assert!(
        center[2] > center[0],
        "centre pixel {center:?} (BGRA) should be the reddish placeholder cube"
    );
    assert_eq!(
        corner[3],
        0,
        "corner pixel {corner:?} (BGRA) should be the transparent clear colour Unity composites over"
    );

    assert_eq!(unity_bridge::bm_shutdown(), BmResult::Ok);
}

#[test]
fn rejects_calls_before_init_and_bad_arguments() {
    let _guard = BRIDGE_LOCK.lock();
    assert_eq!(unity_bridge::bm_shutdown(), BmResult::Ok);

    let mut status = BmFrameStatus {
        completed_frame: 0,
        completed_buffer: 0,
        in_flight: 0,
    };
    // SAFETY: valid out pointer, bridge not initialized.
    assert_eq!(
        unsafe { unity_bridge::bm_begin_frame(std::ptr::from_mut(&mut status)) },
        BmResult::NotInitialized
    );
    // SAFETY: null is checked before use.
    assert_eq!(
        unsafe { unity_bridge::bm_init(std::ptr::null()) },
        BmResult::InvalidArgument
    );

    let mut mismatched = config(BmOutputKind::Offscreen);
    mismatched.abi_version = abi::ABI_VERSION + 1;
    // SAFETY: valid config pointer.
    assert_eq!(
        unsafe { unity_bridge::bm_init(std::ptr::from_ref(&mismatched)) },
        BmResult::AbiMismatch
    );

    let mut empty = config(BmOutputKind::Offscreen);
    empty.width = 0;
    // SAFETY: valid config pointer.
    assert_eq!(
        unsafe { unity_bridge::bm_init(std::ptr::from_ref(&empty)) },
        BmResult::InvalidArgument
    );

    #[cfg(not(target_os = "macos"))]
    {
        // SAFETY: valid config pointer.
        assert_eq!(
            unsafe { unity_bridge::bm_init(std::ptr::from_ref(&config(BmOutputKind::IoSurface))) },
            BmResult::Unsupported
        );
    }

    assert!(!unity_bridge::bm_get_render_event_func().is_null());
}
