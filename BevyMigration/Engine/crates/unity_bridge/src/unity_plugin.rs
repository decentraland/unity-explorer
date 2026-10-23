//! Unity's native plugin interface (`IUnityInterfaces`), used only to reach the Metal device Unity
//! renders with so the shared surfaces can be wrapped on both devices (plan § 8 "Output sharing").
//! Layouts follow `PluginAPI/IUnityInterface.h`, `IUnityGraphics.h` and `IUnityGraphicsMetal.h`.

use std::{
    ffi::c_void,
    sync::atomic::{AtomicI32, AtomicPtr, Ordering},
};

/// `IUnityGraphicsMetalV1` registered GUID.
const METAL_V1_GUID_HIGH: u64 = 0x29F8_F3D0_3833_465E;
const METAL_V1_GUID_LOW: u64 = 0x9213_8551_C15D_823D;
/// `IUnityGraphics` registered GUID.
const GRAPHICS_GUID_HIGH: u64 = 0x7CBA_0A9C_A4DD_B544;
const GRAPHICS_GUID_LOW: u64 = 0x8C5A_D492_6EB1_7B11;
/// `kUnityGfxRendererMetal`.
pub const RENDERER_METAL: i32 = 16;

#[repr(C)]
struct IUnityInterfaces {
    get_interface: Option<unsafe extern "C" fn(guid_high: u64, guid_low: u64) -> *mut c_void>,
    register_interface:
        Option<unsafe extern "C" fn(guid_high: u64, guid_low: u64, ptr: *mut c_void)>,
    get_interface_split: Option<unsafe extern "C" fn(guid_high: u64, guid_low: u64) -> *mut c_void>,
    register_interface_split:
        Option<unsafe extern "C" fn(guid_high: u64, guid_low: u64, ptr: *mut c_void)>,
}

/// First two entries of `IUnityGraphicsMetalV1`; the rest is never read.
#[repr(C)]
struct IUnityGraphicsMetalV1 {
    metal_bundle: Option<unsafe extern "C" fn() -> *mut c_void>,
    metal_device: Option<unsafe extern "C" fn() -> *mut c_void>,
}

/// First entry of `IUnityGraphics`.
#[repr(C)]
struct IUnityGraphics {
    get_renderer: Option<unsafe extern "C" fn() -> i32>,
}

static UNITY_METAL_DEVICE: AtomicPtr<c_void> = AtomicPtr::new(std::ptr::null_mut());
static UNITY_RENDERER: AtomicI32 = AtomicI32::new(-1);

/// Called by Unity when the plugin is loaded, before any `bm_*` call.
///
/// # Safety
/// `interfaces` must be the `IUnityInterfaces*` Unity passes, or null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn UnityPluginLoad(interfaces: *mut c_void) {
    if interfaces.is_null() {
        return;
    }

    // SAFETY: Unity passes a valid IUnityInterfaces that outlives the plugin.
    let registry = unsafe { &*interfaces.cast::<IUnityInterfaces>() };
    let Some(get_interface_split) = registry.get_interface_split else {
        return;
    };

    // SAFETY: calling Unity's registry with the documented split-GUID signature.
    let graphics = unsafe { get_interface_split(GRAPHICS_GUID_HIGH, GRAPHICS_GUID_LOW) };

    if !graphics.is_null() {
        // SAFETY: a non-null IUnityGraphics whose first entry is GetRenderer.
        if let Some(get_renderer) = unsafe { (*graphics.cast::<IUnityGraphics>()).get_renderer } {
            // SAFETY: documented as thread safe.
            UNITY_RENDERER.store(unsafe { get_renderer() }, Ordering::Release);
        }
    }

    // SAFETY: as above, for the Metal interface.
    let metal = unsafe { get_interface_split(METAL_V1_GUID_HIGH, METAL_V1_GUID_LOW) };

    if metal.is_null() {
        return;
    }

    // SAFETY: a non-null IUnityGraphicsMetalV1 whose second entry is MetalDevice.
    if let Some(metal_device) = unsafe { (*metal.cast::<IUnityGraphicsMetalV1>()).metal_device } {
        // SAFETY: returns the id<MTLDevice> Unity renders with; retained by Unity for the process lifetime.
        UNITY_METAL_DEVICE.store(unsafe { metal_device() }, Ordering::Release);
    }
}

/// Called by Unity before the plugin is unloaded (never in the Editor).
#[unsafe(no_mangle)]
pub extern "C" fn UnityPluginUnload() {
    UNITY_METAL_DEVICE.store(std::ptr::null_mut(), Ordering::Release);
    UNITY_RENDERER.store(-1, Ordering::Release);
}

/// The `id<MTLDevice>` Unity renders with, null when the plugin was not loaded by Unity or Unity
/// is not on Metal.
pub fn unity_metal_device() -> *mut c_void {
    UNITY_METAL_DEVICE.load(Ordering::Acquire)
}

/// Unity's graphics API id (`UnityGfxRenderer`), -1 when unknown.
pub fn unity_renderer() -> i32 {
    UNITY_RENDERER.load(Ordering::Acquire)
}
