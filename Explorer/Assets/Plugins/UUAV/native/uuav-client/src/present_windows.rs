//! Client-side presentation (D3D11): opens the shared keyed-mutex NV12
//! slots announced by the helper, and on Unity's render event copies the
//! latest published slot into a client-owned NV12 presentation texture on
//! Unity's device — the same stable-pointer contract (one NV12 texture the
//! engine casts its R8/RG8 views over, retire-grace on resolution change)
//! the in-process plugin had, so the C# poll-and-rewrap flow is untouched.

use crate::platform::UnityDevice;
use anyhow::{Context as _, Result, anyhow, ensure};
use std::os::raw::c_void;
use windows::Win32::Foundation::{CloseHandle, HANDLE};
use windows::Win32::Graphics::Direct3D11::{
    D3D11_BIND_SHADER_RESOURCE, D3D11_TEXTURE2D_DESC, D3D11_USAGE_DEFAULT, ID3D11Texture2D,
};
use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_NV12, DXGI_SAMPLE_DESC};
use windows::Win32::Graphics::Dxgi::IDXGIKeyedMutex;
use windows::core::Interface as _;

const SLOTS: usize = 3;

/// A generation announced by the helper but not yet opened. The NT handle
/// values arrive already duplicated into this process and are owned here.
struct PendingSet {
    generation: u32,
    width: u32,
    height: u32,
    handles: Vec<u64>,
}

impl Drop for PendingSet {
    fn drop(&mut self) {
        // a superseded set is never opened; its handles must not leak
        close_handles(&self.handles);
    }
}

/// One shared slot opened on Unity's device.
struct Slot {
    texture: ID3D11Texture2D,
    mutex: IDXGIKeyedMutex,
}

/// An opened generation: every slot texture + keyed mutex on Unity's device.
struct ActiveGen {
    generation: u32,
    width: u32,
    height: u32,
    slots: Vec<Slot>,
}

/// The presentation texture C# wraps, plus the size it was created for.
struct SizedTexture {
    texture: ID3D11Texture2D,
    width: u32,
    height: u32,
}

/// Per-player video state behind the mirror's mutex. Written by the IO
/// thread (announcements, publishes), consumed on Unity's render thread.
#[derive(Default)]
pub struct PlayerVideo {
    pending: Option<PendingSet>,
    active: Option<ActiveGen>,
    presentation: Option<SizedTexture>,
    /// Previous presentation texture, kept alive for one resolution change
    /// so the pointer C# still wraps stays valid until its next poll.
    retired: Option<SizedTexture>,
    /// Newest (generation, slot) the helper finished writing.
    published: Option<(u32, u8)>,
    presented: Option<(u32, u8)>,
    /// Ack the render event owes the helper after opening a generation.
    ack_due: Option<u32>,
}

// D3D11 interfaces are free-threaded; everything here is accessed behind
// the mirror's mutex.
#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for PlayerVideo {}

impl PlayerVideo {
    /// The zmq `TextureSet` announcement for a generation.
    pub fn store_texture_set(
        &mut self,
        generation: u32,
        width: u32,
        height: u32,
        handles: Vec<u64>,
    ) {
        // a newer announcement supersedes whatever was pending; the
        // superseded set's Drop closes its handles
        self.pending = Some(PendingSet {
            generation,
            width,
            height,
            handles,
        });
    }

    pub const fn store_published(&mut self, generation: u32, slot: u8) {
        self.published = Some((generation, slot));
    }

    /// The stable presentation-texture pointer C# wraps, once the first
    /// frame was presented. One NV12 texture covers both planes; `plane` is
    /// part of the fixed C ABI but only Metal consumes it (same contract as
    /// the in-process plugin).
    pub fn texture_ptr(&self, _plane: i32) -> Result<*const c_void, String> {
        self.presentation
            .as_ref()
            .map(|sized| sized.texture.as_raw().cast_const())
            .ok_or_else(|| "video texture is not available yet".to_owned())
    }

    /// Render-thread presentation: opens a pending generation, then copies
    /// the latest published slot into the presentation texture. Returns the
    /// generation to ack, if one was just opened.
    pub fn present(&mut self, unity: &UnityDevice) -> Result<Option<u32>> {
        let mut ack = None;
        if let Some(pending) = self.pending.take() {
            let generation = pending.generation;
            self.active = Some(open_generation(unity, pending)?);
            self.ack_due = Some(generation);
        }
        if self.ack_due.is_some() {
            ack = self.ack_due.take();
        }

        let Some((generation, slot)) = self.published else {
            return Ok(ack);
        };
        if self.presented == Some((generation, slot)) {
            return Ok(ack);
        }
        let Some((active_generation, width, height)) = self
            .active
            .as_ref()
            .map(|active| (active.generation, active.width, active.height))
        else {
            return Ok(ack);
        };
        if active_generation != generation {
            // publish for a generation this side hasn't opened (yet, or
            // anymore); the helper keeps publishing the acked one
            return Ok(ack);
        }

        self.ensure_presentation(unity, width, height)?;
        let Some(active) = self.active.as_ref() else {
            return Ok(ack);
        };
        let presentation = self
            .presentation
            .as_ref()
            .ok_or_else(|| anyhow!("presentation texture is missing"))?;
        let source = active
            .slots
            .get(slot as usize)
            .ok_or_else(|| anyhow!("published slot {slot} out of range"))?;

        if copy_slot(unity, source, &presentation.texture)? {
            self.presented = Some((generation, slot));
        }
        // busy mutex: the helper is mid-write; keep the previous frame and
        // retry on the next render event
        Ok(ack)
    }

    fn ensure_presentation(&mut self, unity: &UnityDevice, width: u32, height: u32) -> Result<()> {
        if self
            .presentation
            .as_ref()
            .is_some_and(|sized| sized.width == width && sized.height == height)
        {
            return Ok(());
        }
        let texture = new_presentation(unity, width, height)?;
        // the generation before last dies here; C# has had a full poll
        // cycle to stop wrapping it
        let _previous = self.retired.take();
        self.retired = self.presentation.take();
        self.presentation = Some(SizedTexture {
            texture,
            width,
            height,
        });
        Ok(())
    }
}

/// Best-effort close of duplicated NT handle values that will never be
/// opened (superseded generations, announcements for gone players).
pub fn close_handles(handles: &[u64]) {
    for &value in handles {
        unsafe {
            _ = CloseHandle(as_handle(value));
        }
    }
}

const fn as_handle(value: u64) -> HANDLE {
    HANDLE(value as usize as *mut c_void)
}

/// Opens every slot of an announced generation on Unity's device. All
/// handles are closed on the way out, opened or not (the resources stay
/// alive through their own COM refs).
fn open_generation(unity: &UnityDevice, mut pending: PendingSet) -> Result<ActiveGen> {
    ensure!(
        pending.handles.len() == SLOTS,
        "expected {SLOTS} slot handles, got {}",
        pending.handles.len()
    );
    let handles = std::mem::take(&mut pending.handles);

    let mut slots = Vec::with_capacity(SLOTS);
    let mut failure = None;
    for &value in &handles {
        if failure.is_none() {
            match open_slot(unity, as_handle(value)) {
                Ok(slot) => slots.push(slot),
                Err(e) => failure = Some(e),
            }
        }
        unsafe {
            _ = CloseHandle(as_handle(value));
        }
    }
    if let Some(e) = failure {
        return Err(e);
    }
    Ok(ActiveGen {
        generation: pending.generation,
        width: pending.width,
        height: pending.height,
        slots,
    })
}

fn open_slot(unity: &UnityDevice, handle: HANDLE) -> Result<Slot> {
    let texture: ID3D11Texture2D = unsafe { unity.device.OpenSharedResource1(handle) }
        .context("OpenSharedResource1 failed")?;
    let mutex: IDXGIKeyedMutex = texture
        .cast()
        .context("shared texture exposes no IDXGIKeyedMutex")?;
    Ok(Slot { texture, mutex })
}

/// The client-owned NV12 texture C# samples from, same shape as the core's
/// in-process presentation texture.
fn new_presentation(unity: &UnityDevice, width: u32, height: u32) -> Result<ID3D11Texture2D> {
    let desc = D3D11_TEXTURE2D_DESC {
        Width: width,
        Height: height,
        MipLevels: 1,
        ArraySize: 1,
        Format: DXGI_FORMAT_NV12,
        SampleDesc: DXGI_SAMPLE_DESC {
            Count: 1,
            Quality: 0,
        },
        Usage: D3D11_USAGE_DEFAULT,
        BindFlags: D3D11_BIND_SHADER_RESOURCE.0 as u32,
        CPUAccessFlags: 0,
        MiscFlags: 0,
    };
    let mut created: Option<ID3D11Texture2D> = None;
    unsafe { unity.device.CreateTexture2D(&desc, None, Some(&mut created)) }
        .with_context(|| format!("CreateTexture2D(NV12 {width}x{height}) failed"))?;
    created.context("CreateTexture2D returned no texture")
}

/// Copies one shared slot into the presentation texture under its keyed
/// mutex. `Ok(false)` when the helper still holds the slot: keep the
/// previous frame, retry next render event.
fn copy_slot(unity: &UnityDevice, slot: &Slot, presentation: &ID3D11Texture2D) -> Result<bool> {
    if !acquire_sync(&slot.mutex, 0, 0)? {
        return Ok(false);
    }
    // same size; a full-subresource copy covers both NV12 planes, recorded
    // on Unity's immediate context (this runs on Unity's render thread)
    unsafe {
        unity
            .context
            .CopySubresourceRegion(presentation, 0, 0, 0, 0, &slot.texture, 0, None);
    }
    unsafe { slot.mutex.ReleaseSync(0) }.context("ReleaseSync failed")?;
    Ok(true)
}

/// `AcquireSync` through the raw vtable: the windows crate folds all success
/// codes into `Ok(())`, making `WAIT_TIMEOUT` (0x102, "still held by the
/// peer") indistinguishable from a real acquisition. `Ok(false)` = busy.
fn acquire_sync(mutex: &IDXGIKeyedMutex, key: u64, timeout_ms: u32) -> Result<bool> {
    const WAIT_TIMEOUT: i32 = 0x102;
    let hr = unsafe {
        (windows::core::Interface::vtable(mutex).AcquireSync)(
            windows::core::Interface::as_raw(mutex),
            key,
            timeout_ms,
        )
    };
    if hr.0 == WAIT_TIMEOUT {
        return Ok(false);
    }
    // WAIT_ABANDONED (0x80) also lands here as acquired: the peer died
    // holding the mutex — ownership transferred, the contents are just stale
    hr.ok().context("AcquireSync failed")?;
    Ok(true)
}
