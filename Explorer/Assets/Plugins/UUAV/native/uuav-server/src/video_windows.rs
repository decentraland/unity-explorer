//! The video half of the adapter: drives the core's render events, copies
//! its presentation texture into cross-process shared slots, and publishes
//! frame availability to the client.
//!
//! Per player: a generation of 3 shared NV12 slot textures created with
//! `SHARED_NTHANDLE | SHARED_KEYEDMUTEX` on the helper's device (the same
//! device the core presents on). Each slot's NT handle stays open in this
//! process and its value is announced inline in `TextureSet`; the client
//! pulls the handles out of this process with `DuplicateHandle` (the
//! sandboxed helper cannot push them into Unity's). A generation becomes
//! active on `TextureSetAck`, and until then the previous one keeps
//! publishing, mirroring the core's own retire-grace on resolution change.

use crate::device::ProbeDevice;
use crate::state;
use anyhow::{Context as _, Result, anyhow};
use std::collections::HashMap;
use std::os::raw::c_void;
use uuav_core as core;
use uuav_ipc::channel::Channel;
use uuav_ipc::protocol::{PlayerId, ToClient};
use windows::Win32::Foundation::{CloseHandle, HANDLE};
use windows::Win32::Graphics::Direct3D11::{
    D3D11_BIND_SHADER_RESOURCE, D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX,
    D3D11_RESOURCE_MISC_SHARED_NTHANDLE, D3D11_TEXTURE2D_DESC, D3D11_USAGE_DEFAULT, ID3D11Device,
    ID3D11DeviceContext, ID3D11Resource, ID3D11Texture2D,
};
use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_NV12, DXGI_SAMPLE_DESC};
use windows::Win32::Graphics::Dxgi::{
    DXGI_SHARED_RESOURCE_READ, DXGI_SHARED_RESOURCE_WRITE, IDXGIKeyedMutex, IDXGIResource1,
};
use windows::core::{Interface as _, PCWSTR};

const SLOTS: usize = 3;

/// How long one copy may wait for the client to release a slot. The 3-slot
/// rotation makes contention rare; a stalled client costs at most this per
/// tick (the frame is skipped, not queued).
const ACQUIRE_TIMEOUT_MS: u32 = 2;

struct Slot {
    texture: ID3D11Texture2D,
    mutex: IDXGIKeyedMutex,
    /// The shared NT handle whose value was announced to the client; must
    /// stay open here until the client pulled it (see the graveyard in
    /// [`PlayerVideo`] for the un-acked retire path).
    shared: HANDLE,
}

impl Drop for Slot {
    fn drop(&mut self) {
        unsafe { _ = CloseHandle(self.shared) };
    }
}

struct GenSlots {
    generation: u32,
    width: u32,
    height: u32,
    slots: [Slot; SLOTS],
    next_slot: usize,
}

impl GenSlots {
    const fn matches(&self, width: u32, height: u32) -> bool {
        self.width == width && self.height == height
    }
}

#[derive(Default)]
struct PlayerVideo {
    next_generation: u32,
    /// Announced and acked: the generation frames are published into.
    active: Option<GenSlots>,
    /// Announced, awaiting `TextureSetAck`; `active` keeps publishing.
    pending: Option<GenSlots>,
    /// Announced-then-superseded generations whose shared handles the
    /// client may not have pulled yet; cleared once an ack proves the
    /// client consumed every earlier announcement (the channel is
    /// in-order). Bounded by resolution changes between acks.
    superseded: Vec<GenSlots>,
}

pub struct VideoPump {
    device: ID3D11Device,
    context: ID3D11DeviceContext,
    render_event: core::UUAVRenderEvent,
    players: HashMap<PlayerId, PlayerVideo>,
}

// D3D11 devices are free-threaded and the core multithread-protects the
// immediate context; the pump only runs on the serve-loop thread anyway.
#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for VideoPump {}

impl VideoPump {
    pub fn new(device: &ProbeDevice) -> Result<Self> {
        let device = device.device().clone();
        let context = unsafe { device.GetImmediateContext() }
            .context("device has no immediate context")?;
        Ok(Self {
            device,
            context,
            render_event: core::uuav_get_render_callback(),
            players: HashMap::new(),
        })
    }

    /// One video tick over all live players: render event, generation
    /// management, slot copy, publish.
    pub fn tick(&mut self, ids: impl Iterator<Item = PlayerId>, channel: &mut Channel) {
        for id in ids {
            if let Err(e) = self.tick_player(id, channel) {
                // per-player video hiccups are not protocol failures; the
                // helper's own logs are the diagnostic channel
                eprintln!("uuav-helper: video tick for player {id}: {e}");
            }
        }
    }

    pub fn ack(&mut self, id: PlayerId, generation: u32) {
        if let Some(player) = self.players.get_mut(&id)
            && player
                .pending
                .as_ref()
                .is_some_and(|pending| pending.generation == generation)
        {
            // the client opened the new set; switch over (the old slot
            // textures stay alive through the client's own COM refs
            // until it drops them). This ack also proves the client
            // consumed every earlier announcement, so the superseded
            // generations' shared handles can finally close.
            player.active = player.pending.take();
            player.superseded.clear();
        }
    }

    pub fn remove_player(&mut self, id: PlayerId) {
        self.players.remove(&id);
    }

    fn tick_player(&mut self, id: PlayerId, channel: &mut Channel) -> Result<()> {
        // the core presents the due frame into its presentation texture,
        // exactly as it would for Unity's render thread
        (self.render_event)(id as i32);

        let Some((core_texture, width, height)) = query_core_texture(id) else {
            // no frame presented yet (opening, audio-only, closed)
            return Ok(());
        };

        let player = self.players.entry(id).or_default();

        if !player.active.as_ref().is_some_and(|g| g.matches(width, height))
            && !player.pending.as_ref().is_some_and(|g| g.matches(width, height))
        {
            player.next_generation += 1;
            let generation = player.next_generation;
            let (slots, handles) = create_generation(&self.device, width, height)?;
            channel.send(&ToClient::TextureSet {
                id,
                generation,
                width,
                height,
                handles,
            })?;
            let created = GenSlots {
                generation,
                width,
                height,
                slots,
                next_slot: 0,
            };
            if player.active.is_none() {
                // nothing to keep publishing into; activate optimistically —
                // publishes are ignored by the client until it opens and acks
                player.active = Some(created);
            } else {
                // an un-acked pending set was announced but possibly not
                // pulled by the client yet; park it so its shared handles
                // survive until an ack proves the announcement was consumed
                if let Some(replaced) = player.pending.take() {
                    player.superseded.push(replaced);
                }
                player.pending = Some(created);
            }
        }

        let Some(active) = player.active.as_mut() else {
            return Ok(());
        };
        if !active.matches(width, height) {
            // resolution changed and the new generation is still pending
            // ack; keep the last published frame instead of writing stale
            // dimensions into the active slots
            return Ok(());
        }

        let slot_index = active.next_slot;
        active.next_slot = (active.next_slot + 1) % SLOTS;
        let slot = active
            .slots
            .get(slot_index)
            .ok_or_else(|| anyhow!("slot index {slot_index} out of range"))?;

        if !copy_into_slot(&self.context, core_texture, slot)? {
            // the client still holds the slot; skip this frame
            return Ok(());
        }

        channel.send(&ToClient::FramePublished {
            id,
            generation: active.generation,
            slot: slot_index as u8,
        })
    }
}

/// The core's presentation texture pointer + visible size, or `None` while
/// unavailable (mirrors what Unity's poll sees). Dimensions are folded to
/// even like the core's own NV12 texture, so slot sizes always match it.
fn query_core_texture(id: PlayerId) -> Option<(*const c_void, u32, u32)> {
    let mut texture: *const c_void = std::ptr::null();
    state::consume_result(unsafe { core::uuav_player_get_video_texture(id, 0, &mut texture) })
        .ok()?;

    let mut size = core::VideoSize {
        width: 0,
        height: 0,
    };
    state::consume_result(unsafe { core::uuav_player_get_video_size(id, &mut size) }).ok()?;
    let width = size.width & !1;
    let height = size.height & !1;
    if texture.is_null() || width == 0 || height == 0 {
        return None;
    }
    Some((texture, width, height))
}

/// One generation of shared slots plus their NT handle values in *this*
/// process (the slots own the handles; the client pulls copies with
/// `DuplicateHandle` when the announcement arrives).
fn create_generation(
    device: &ID3D11Device,
    width: u32,
    height: u32,
) -> Result<([Slot; SLOTS], Vec<u64>)> {
    let mut handles = Vec::with_capacity(SLOTS);
    let mut make = || -> Result<Slot> {
        let (slot, handle) = create_slot(device, width, height)?;
        handles.push(handle);
        Ok(slot)
    };
    let slots = [make()?, make()?, make()?];
    Ok((slots, handles))
}

/// One shared keyed-mutex NV12 slot texture; returns it together with its
/// NT handle value in this process.
fn create_slot(device: &ID3D11Device, width: u32, height: u32) -> Result<(Slot, u64)> {
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
        MiscFlags: (D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX.0
            | D3D11_RESOURCE_MISC_SHARED_NTHANDLE.0) as u32,
    };
    let mut created: Option<ID3D11Texture2D> = None;
    unsafe { device.CreateTexture2D(&desc, None, Some(&mut created)) }
        .with_context(|| format!("CreateTexture2D(shared NV12 {width}x{height}) failed"))?;
    let texture = created.context("CreateTexture2D returned no texture")?;

    let mutex: IDXGIKeyedMutex = texture
        .cast()
        .context("shared texture exposes no IDXGIKeyedMutex")?;
    let resource: IDXGIResource1 = texture
        .cast()
        .context("shared texture exposes no IDXGIResource1")?;

    // keyed-mutex resources must be opened with both read and write access
    let shared = unsafe {
        resource.CreateSharedHandle(
            None,
            (DXGI_SHARED_RESOURCE_READ | DXGI_SHARED_RESOURCE_WRITE).0,
            PCWSTR::null(),
        )
    }
    .context("CreateSharedHandle failed")?;

    let value = shared.0 as usize as u64;
    Ok((
        Slot {
            texture,
            mutex,
            shared,
        },
        value,
    ))
}

/// Copies the core's presentation texture into one shared slot under its
/// keyed mutex. `Ok(false)` when the client still holds the slot: the
/// publish is skipped and the next tick writes the next slot.
fn copy_into_slot(
    context: &ID3D11DeviceContext,
    core_texture: *const c_void,
    slot: &Slot,
) -> Result<bool> {
    // raw ID3D11Texture2D* straight from the core; valid through this tick
    // (the texture is only replaced inside the render event this same
    // thread issues)
    let raw = core_texture.cast_mut();
    let src = unsafe { ID3D11Resource::from_raw_borrowed(&raw) }
        .ok_or_else(|| anyhow!("core presentation texture is null"))?;

    if !acquire_sync(&slot.mutex, 0, ACQUIRE_TIMEOUT_MS)? {
        return Ok(false);
    }
    // same size on the same device; a full-subresource copy covers both
    // NV12 planes (the core multithread-protects the immediate context, so
    // FFmpeg's decoder threads cannot corrupt this call)
    unsafe {
        context.CopySubresourceRegion(&slot.texture, 0, 0, 0, 0, src, 0, None);
    }
    let released = unsafe { slot.mutex.ReleaseSync(0) };
    // the release orders the client's acquire against the queued copy;
    // flush so the GPU work is submitted before the publish message
    unsafe {
        context.Flush();
    }
    released.context("ReleaseSync failed")?;
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
