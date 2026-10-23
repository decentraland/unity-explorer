// The workspace denies `unsafe`; calling the extern "C" surface is what this test is for.
#![allow(unsafe_code)]
//! The whole ABI path without Unity: init offscreen, submit a snapshot with one cube in front of
//! the camera, wait for the frame, read the pixels back. Needs a GPU; the Metal path is what the
//! macOS development machines have.

use abi::{
    BmCamera, BmFrameStatus, BmInitConfig, BmMat4, BmOutputKind, BmResult, BmSnapshot,
    MESH_PLACEHOLDER_CUBE, renderer_flags,
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
        renderer_count: u32::try_from(handles.len()).unwrap_or(0),
        handles: handles.as_ptr(),
        worlds: worlds.as_ptr(),
        meshes: meshes.as_ptr(),
        flags: flags.as_ptr(),
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

    // The placeholder material is orange: more red than blue in the centre, unlike the blue-grey clear colour.
    // BGRA: index 2 is red, index 0 is blue.
    assert!(
        center[2] > center[0],
        "centre pixel {center:?} (BGRA) should be reddish"
    );
    assert!(
        corner[0] >= corner[2],
        "corner pixel {corner:?} (BGRA) should be the blue-grey clear colour"
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
