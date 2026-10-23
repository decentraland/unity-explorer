//! The cdylib Unity loads (BevyMigration/docs/README.md § 7 step 2 and § 8). Owns a Bevy `App` on its
//! own thread, ingests snapshots, and shares its output with the host.
//!
//! This crate is the FFI boundary and the one place in the workspace allowed to hold `unsafe` code:
//! every entry point validates its pointers, copies what it needs out of host memory before returning,
//! and runs under `catch_unwind` so a panic becomes [`BmResult::Panicked`] and a log line (plan § 12).
#![allow(unsafe_code)]

mod app;
mod bridge;
mod logging;
mod output;
mod unity_plugin;

use abi::{
    BmFrameStatus, BmInitConfig, BmOutputTexture, BmResourceCmd, BmResourceCmdKind, BmResult,
    BmSnapshot,
};
use bridge::Bridge;
use parking_lot::{Mutex, MutexGuard};
use render::snapshot::{RendererArrays, Snapshot, SnapshotCamera};
use std::{
    ffi::c_void,
    panic::{AssertUnwindSafe, catch_unwind},
};

static BRIDGE: Mutex<Option<Bridge>> = Mutex::new(None);

/// ABI version this library was built against; the host compares it with its generated bindings.
#[unsafe(no_mangle)]
pub extern "C" fn bm_abi_version() -> u32 {
    abi::ABI_VERSION
}

/// Starts the bridge: Bevy thread, render device and output buffers. Re-entrant: an existing bridge
/// is shut down first, which is what makes Editor domain reloads safe (plan § 8 "Editor").
///
/// # Safety
/// `config` must be null or point to a readable [`BmInitConfig`].
#[unsafe(no_mangle)]
pub unsafe extern "C" fn bm_init(config: *const BmInitConfig) -> BmResult {
    guarded("bm_init", || {
        if config.is_null() {
            return BmResult::InvalidArgument;
        }

        // SAFETY: checked non-null; the caller guarantees it points to a BmInitConfig.
        let config = unsafe { *config };

        if config.abi_version != abi::ABI_VERSION {
            return BmResult::AbiMismatch;
        }

        logging::install(config.log);
        let mut slot = lock();
        *slot = None;

        match Bridge::start(config) {
            Ok(bridge) => {
                *slot = Some(bridge);
                BmResult::Ok
            }
            Err(result) => result,
        }
    })
}

/// Stops the Bevy thread and releases every output buffer. Safe to call when not initialized.
#[unsafe(no_mangle)]
pub extern "C" fn bm_shutdown() -> BmResult {
    guarded("bm_shutdown", || {
        let bridge = lock().take();
        drop(bridge);
        logging::clear_host();
        BmResult::Ok
    })
}

/// Reports which output buffer holds the most recently completed frame.
///
/// # Safety
/// `status` must be null or point to writable memory for a [`BmFrameStatus`].
#[unsafe(no_mangle)]
pub unsafe extern "C" fn bm_begin_frame(status: *mut BmFrameStatus) -> BmResult {
    with_bridge("bm_begin_frame", |bridge| {
        if status.is_null() {
            return BmResult::InvalidArgument;
        }

        // SAFETY: checked non-null; the caller guarantees writable memory for one BmFrameStatus.
        unsafe { status.write(bridge.frame_status()) };
        BmResult::Ok
    })
}

/// Copies one frame of renderer state out of host memory and queues it for rendering.
///
/// # Safety
/// `snapshot` must be null or point to a [`BmSnapshot`] whose arrays hold `renderer_count` elements.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn bm_submit_snapshot(snapshot: *const BmSnapshot) -> BmResult {
    with_bridge("bm_submit_snapshot", |bridge| {
        if snapshot.is_null() {
            return BmResult::InvalidArgument;
        }

        // SAFETY: checked non-null; the caller guarantees it points to a BmSnapshot.
        let raw = unsafe { &*snapshot };

        // SAFETY: the caller guarantees `renderer_count` elements behind each array.
        let Some(snapshot) = (unsafe { copy_snapshot(raw) }) else {
            return BmResult::InvalidArgument;
        };

        bridge.submit(snapshot)
    })
}

/// Resource upload entry point; the first command kinds land with step 3.
///
/// # Safety
/// `cmd` must be null or point to a readable [`BmResourceCmd`].
#[unsafe(no_mangle)]
pub unsafe extern "C" fn bm_resource_cmd(cmd: *const BmResourceCmd) -> BmResult {
    with_bridge("bm_resource_cmd", |_| {
        if cmd.is_null() {
            return BmResult::InvalidArgument;
        }

        // SAFETY: checked non-null; the caller guarantees it points to a BmResourceCmd.
        let cmd = unsafe { *cmd };

        match cmd.kind {
            BmResourceCmdKind::None => BmResult::InvalidArgument,
        }
    })
}

/// Describes output buffer `index` so the host can wrap it as an external texture.
///
/// # Safety
/// `out` must be null or point to writable memory for a [`BmOutputTexture`].
#[unsafe(no_mangle)]
pub unsafe extern "C" fn bm_get_output(index: u32, out: *mut BmOutputTexture) -> BmResult {
    with_bridge("bm_get_output", |bridge| {
        if out.is_null() {
            return BmResult::InvalidArgument;
        }

        match bridge.output(index) {
            // SAFETY: checked non-null; the caller guarantees writable memory for one BmOutputTexture.
            Some(texture) => unsafe {
                out.write(texture);
                BmResult::Ok
            },
            None => BmResult::InvalidArgument,
        }
    })
}

/// Copies the latest completed frame's pixels (tightly packed BGRA8, sRGB) into `out`; `written`
/// receives the byte count needed. Only [`abi::BmOutputKind::Offscreen`] keeps pixels on the CPU.
///
/// # Safety
/// `out` must be null or point to `len` writable bytes; `written` must be null or writable.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn bm_read_output(out: *mut u8, len: usize, written: *mut usize) -> BmResult {
    with_bridge("bm_read_output", |bridge| {
        let Some(pixels) = bridge.latest_pixels() else {
            return BmResult::Unsupported;
        };

        if !written.is_null() {
            // SAFETY: checked non-null; the caller guarantees it is writable.
            unsafe { written.write(pixels.data.len()) };
        }

        if out.is_null() || len < pixels.data.len() {
            return BmResult::InvalidArgument;
        }

        // SAFETY: `out` is non-null with at least `pixels.data.len()` writable bytes.
        unsafe { out.copy_from_nonoverlapping(pixels.data.as_ptr(), pixels.data.len()) };
        BmResult::Ok
    })
}

/// Unity render-thread callback, issued through `GL.IssuePluginEvent` with the function pointer
/// from [`bm_get_render_event_func`]. Step 2 only counts the events; the host-side texture
/// publication it will own arrives with the Windows path in step 5.
#[unsafe(no_mangle)]
pub extern "C" fn bm_render_event(event_id: i32) {
    let _ = guarded("bm_render_event", || {
        if let Some(bridge) = lock().as_ref() {
            bridge.note_render_event(event_id);
        }

        BmResult::Ok
    });
}

/// The pointer `GL.IssuePluginEvent` needs.
#[unsafe(no_mangle)]
pub extern "C" fn bm_get_render_event_func() -> *const c_void {
    bm_render_event as *const c_void
}

/// Number of render events received since init; a diagnostic for the composite feature.
#[unsafe(no_mangle)]
pub extern "C" fn bm_render_event_count() -> u64 {
    lock().as_ref().map_or(0, Bridge::render_events)
}

fn lock() -> MutexGuard<'static, Option<Bridge>> {
    BRIDGE.lock()
}

fn guarded(name: &str, body: impl FnOnce() -> BmResult) -> BmResult {
    match catch_unwind(AssertUnwindSafe(body)) {
        Ok(result) => result,
        Err(payload) => {
            logging::error(&format!(
                "{name} panicked: {}",
                logging::panic_message(payload.as_ref())
            ));
            BmResult::Panicked
        }
    }
}

fn with_bridge(name: &str, body: impl FnOnce(&mut Bridge) -> BmResult) -> BmResult {
    guarded(name, || match lock().as_mut() {
        Some(bridge) => body(bridge),
        None => BmResult::NotInitialized,
    })
}

/// # Safety
/// Each non-null array in `raw` must hold `raw.renderer_count` readable elements.
unsafe fn copy_snapshot(raw: &BmSnapshot) -> Option<Snapshot> {
    let count = usize::try_from(raw.renderer_count).ok()?;

    if count > 0
        && (raw.handles.is_null()
            || raw.worlds.is_null()
            || raw.meshes.is_null()
            || raw.flags.is_null())
    {
        return None;
    }

    let arrays = if count == 0 {
        RendererArrays {
            handles: &[],
            worlds: &[],
            meshes: &[],
            flags: &[],
        }
    } else {
        // SAFETY: non-null, and the caller guarantees `count` elements behind each pointer.
        unsafe {
            RendererArrays {
                handles: std::slice::from_raw_parts(raw.handles, count),
                worlds: std::slice::from_raw_parts(raw.worlds, count),
                meshes: std::slice::from_raw_parts(raw.meshes, count),
                flags: std::slice::from_raw_parts(raw.flags, count),
            }
        }
    };

    Snapshot::new(
        raw.frame,
        SnapshotCamera::from_abi(&raw.camera),
        bevy::math::Vec3::from_array(raw.ambient),
        bevy::math::Vec3::from_array(raw.sun_direction),
        bevy::math::Vec3::from_array(raw.sun_color),
        arrays,
    )
}
