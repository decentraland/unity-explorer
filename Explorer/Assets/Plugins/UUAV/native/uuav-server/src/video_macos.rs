//! The video half of the adapter: drives the core's render events, copies
//! its presentation planes into cross-process shared IOSurface slots, and
//! publishes frame availability to the client.
//!
//! Per player: a generation of 3 slots x 2 single-plane IOSurfaces (Y
//! `R8Unorm` at w x h, UV `RG8Unorm` at w/2 x h/2). A generation is
//! announced once (ports over the mach channel + `TextureSet` over zmq) and
//! becomes active on `TextureSetAck`; until then the previous generation
//! keeps publishing, mirroring the core's own retire-grace on resolution
//! change.

use crate::device::ProbeDevice;
use crate::state;
use anyhow::{Context as _, Result, anyhow};
use objc2::Message as _;
use objc2::rc::Retained;
use objc2::runtime::ProtocolObject;
use objc2_core_foundation::{CFDictionary, CFNumber, CFRetained, CFString, CFType};
use objc2_io_surface::{
    IOSurfaceRef, kIOSurfaceBytesPerElement, kIOSurfaceBytesPerRow, kIOSurfaceHeight,
    kIOSurfaceWidth,
};
use objc2_metal::{
    MTLBlitCommandEncoder, MTLCommandBuffer, MTLCommandEncoder, MTLCommandQueue, MTLDevice,
    MTLOrigin, MTLPixelFormat, MTLSize, MTLStorageMode, MTLTexture, MTLTextureDescriptor,
};
use std::collections::HashMap;
use std::os::raw::c_void;
use uuav_core as core;
use uuav_ipc::protocol::{PlayerId, ToClient};
use uuav_ipc::{mach_channel, socket, zmq};

type Texture = Retained<ProtocolObject<dyn MTLTexture>>;

const SLOTS: usize = 3;

struct SlotPair {
    y_surface: CFRetained<IOSurfaceRef>,
    uv_surface: CFRetained<IOSurfaceRef>,
    y: Texture,
    uv: Texture,
}

struct GenSlots {
    generation: u32,
    width: u32,
    height: u32,
    slots: [SlotPair; SLOTS],
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
}

pub struct VideoPump {
    device: Retained<ProtocolObject<dyn MTLDevice>>,
    queue: Retained<ProtocolObject<dyn MTLCommandQueue>>,
    mach: mach_channel::Sender,
    render_event: core::UUAVRenderEvent,
    players: HashMap<PlayerId, PlayerVideo>,
}

// Metal devices/queues/textures and IOSurfaces are free-threaded; the pump
// only runs on the serve-loop thread anyway.
#[allow(clippy::non_send_fields_in_send_ty)]
unsafe impl Send for VideoPump {}

impl VideoPump {
    pub fn new(device: &ProbeDevice, service: &str) -> Result<Self> {
        Ok(Self {
            device: device.metal_device().retain(),
            queue: device.command_queue().retain(),
            mach: mach_channel::Sender::look_up(service)?,
            render_event: core::uuav_get_render_callback(),
            players: HashMap::new(),
        })
    }

    /// One video tick over all live players: render event, generation
    /// management, slot copy, publish.
    pub fn tick(&mut self, ids: impl Iterator<Item = PlayerId>, sock: &zmq::Socket) {
        for id in ids {
            if let Err(e) = self.tick_player(id, sock) {
                // per-player video hiccups are not protocol failures; the
                // helper's own logs are the diagnostic channel
                eprintln!("uuav-helper: video tick for player {id}: {e}");
            }
        }
    }

    pub fn ack(&mut self, id: PlayerId, generation: u32) {
        if let Some(player) = self.players.get_mut(&id) {
            if player
                .pending
                .as_ref()
                .is_some_and(|pending| pending.generation == generation)
            {
                // the client wrapped the new set; switch over (the old
                // surfaces stay alive through the client's own retains
                // until it drops them)
                player.active = player.pending.take();
            }
        }
    }

    pub fn remove_player(&mut self, id: PlayerId) {
        self.players.remove(&id);
    }

    fn tick_player(&mut self, id: PlayerId, sock: &zmq::Socket) -> Result<()> {
        // the core presents the due frame into its presentation planes,
        // exactly as it would for Unity's render thread
        (self.render_event)(id as i32);

        let Some((core_y, core_uv, width, height)) = query_core_planes(id) else {
            // no frame presented yet (opening, audio-only, closed)
            return Ok(());
        };

        let player = self.players.entry(id).or_default();

        if !player.active.as_ref().is_some_and(|g| g.matches(width, height))
            && !player.pending.as_ref().is_some_and(|g| g.matches(width, height))
        {
            player.next_generation += 1;
            let generation = player.next_generation;
            let slots = create_generation(&self.device, width, height)?;
            announce(&self.mach, sock, id, generation, width, height, &slots)?;
            let created = GenSlots {
                generation,
                width,
                height,
                slots,
                next_slot: 0,
            };
            if player.active.is_none() {
                // nothing to keep publishing into; activate optimistically —
                // publishes are ignored by the client until it wraps and acks
                player.active = Some(created);
            } else {
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

        blit_planes(
            &self.queue,
            core_y,
            core_uv,
            slot,
            width,
            height,
        )?;

        socket::send(
            sock,
            &ToClient::FramePublished {
                id,
                generation: active.generation,
                slot: slot_index as u8,
            },
        )
    }
}

/// The core's presentation plane pointers + visible size, or `None` while
/// unavailable (mirrors what Unity's poll sees).
fn query_core_planes(id: PlayerId) -> Option<(*const c_void, *const c_void, u32, u32)> {
    let mut y: *const c_void = std::ptr::null();
    state::consume_result(unsafe { core::uuav_player_get_video_texture(id, 0, &mut y) }).ok()?;
    let mut uv: *const c_void = std::ptr::null();
    state::consume_result(unsafe { core::uuav_player_get_video_texture(id, 1, &mut uv) }).ok()?;

    let mut size = core::VideoSize {
        width: 0,
        height: 0,
    };
    state::consume_result(unsafe { core::uuav_player_get_video_size(id, &mut size) }).ok()?;
    if y.is_null() || uv.is_null() || size.width == 0 || size.height == 0 {
        return None;
    }
    Some((y, uv, size.width, size.height))
}

fn create_generation(
    device: &ProtocolObject<dyn MTLDevice>,
    width: u32,
    height: u32,
) -> Result<[SlotPair; SLOTS]> {
    let make_pair = || -> Result<SlotPair> {
        let y_surface = create_surface(width as usize, height as usize, 1)?;
        let uv_surface = create_surface((width / 2) as usize, (height / 2) as usize, 2)?;
        let y = wrap_surface(device, &y_surface, MTLPixelFormat::R8Unorm)?;
        let uv = wrap_surface(device, &uv_surface, MTLPixelFormat::RG8Unorm)?;
        Ok(SlotPair {
            y_surface,
            uv_surface,
            y,
            uv,
        })
    };
    Ok([make_pair()?, make_pair()?, make_pair()?])
}

fn announce(
    mach: &mach_channel::Sender,
    sock: &zmq::Socket,
    id: PlayerId,
    generation: u32,
    width: u32,
    height: u32,
    slots: &[SlotPair; SLOTS],
) -> Result<()> {
    for (slot_index, slot) in slots.iter().enumerate() {
        for (plane, surface) in [(0u8, &slot.y_surface), (1u8, &slot.uv_surface)] {
            let port = surface.create_mach_port();
            mach.send(
                mach_channel::SurfaceTag {
                    player: id,
                    generation,
                    slot: slot_index as u8,
                    plane,
                },
                port,
            )?;
        }
    }
    socket::send(
        sock,
        &ToClient::TextureSet {
            id,
            generation,
            width,
            height,
            // surfaces travel as mach ports, not handles
            handles: Vec::new(),
        },
    )
}

/// One single-plane shared surface (R8 or RG8 by `bytes_per_element`).
#[allow(clippy::cast_possible_wrap)] // video dimensions are nowhere near isize::MAX
fn create_surface(
    width: usize,
    height: usize,
    bytes_per_element: usize,
) -> Result<CFRetained<IOSurfaceRef>> {
    let bytes_per_row =
        unsafe { IOSurfaceRef::align_property(kIOSurfaceBytesPerRow, width * bytes_per_element) };

    let keys: [&CFString; 4] = unsafe {
        [
            kIOSurfaceWidth,
            kIOSurfaceHeight,
            kIOSurfaceBytesPerElement,
            kIOSurfaceBytesPerRow,
        ]
    };
    let values = [
        CFNumber::new_isize(width as isize),
        CFNumber::new_isize(height as isize),
        CFNumber::new_isize(bytes_per_element as isize),
        CFNumber::new_isize(bytes_per_row as isize),
    ];
    let value_refs: [&CFType; 4] = [
        values[0].as_ref(),
        values[1].as_ref(),
        values[2].as_ref(),
        values[3].as_ref(),
    ];
    let properties: CFRetained<CFDictionary<CFString, CFType>> =
        CFDictionary::from_slices(&keys, &value_refs);
    // erase the element types for the untyped IOSurfaceCreate signature
    let properties = unsafe { CFRetained::cast_unchecked::<CFDictionary>(properties) };

    unsafe { IOSurfaceRef::new(&properties) }
        .ok_or_else(|| anyhow!("IOSurfaceCreate({width}x{height} bpe {bytes_per_element}) failed"))
}

/// Server-side `MTLTexture` view over a whole single-plane surface.
fn wrap_surface(
    device: &ProtocolObject<dyn MTLDevice>,
    surface: &IOSurfaceRef,
    format: MTLPixelFormat,
) -> Result<Texture> {
    let descriptor = unsafe {
        MTLTextureDescriptor::texture2DDescriptorWithPixelFormat_width_height_mipmapped(
            format,
            surface.width(),
            surface.height(),
            false,
        )
    };
    // IOSurface-backed textures must be Shared (they alias CPU-visible memory)
    descriptor.setStorageMode(MTLStorageMode::Shared);
    device
        .newTextureWithDescriptor_iosurface_plane(&descriptor, surface, 0)
        .ok_or_else(|| anyhow!("wrapping shared surface as {format:?} failed"))
}

/// Copies the core's presentation planes into one shared slot,
/// synchronously: the publish that follows must only ever announce
/// completed GPU work (that completion is the cross-process sync).
fn blit_planes(
    queue: &ProtocolObject<dyn MTLCommandQueue>,
    core_y: *const c_void,
    core_uv: *const c_void,
    slot: &SlotPair,
    width: u32,
    height: u32,
) -> Result<()> {
    // raw id<MTLTexture> pointers straight from the core; valid through
    // this tick (the core retires replaced planes for a full poll cycle)
    let src_y: &ProtocolObject<dyn MTLTexture> = unsafe { &*core_y.cast() };
    let src_uv: &ProtocolObject<dyn MTLTexture> = unsafe { &*core_uv.cast() };

    let buffer = queue
        .commandBuffer()
        .context("command queue returned no command buffer")?;
    let encoder = buffer
        .blitCommandEncoder()
        .context("command buffer returned no blit encoder")?;

    let origin = MTLOrigin { x: 0, y: 0, z: 0 };
    unsafe {
        encoder.copyFromTexture_sourceSlice_sourceLevel_sourceOrigin_sourceSize_toTexture_destinationSlice_destinationLevel_destinationOrigin(
            src_y,
            0,
            0,
            origin,
            MTLSize {
                width: width as usize,
                height: height as usize,
                depth: 1,
            },
            &slot.y,
            0,
            0,
            origin,
        );
        encoder.copyFromTexture_sourceSlice_sourceLevel_sourceOrigin_sourceSize_toTexture_destinationSlice_destinationLevel_destinationOrigin(
            src_uv,
            0,
            0,
            origin,
            MTLSize {
                width: (width / 2) as usize,
                height: (height / 2) as usize,
                depth: 1,
            },
            &slot.uv,
            0,
            0,
            origin,
        );
    }
    encoder.endEncoding();
    buffer.commit();
    buffer.waitUntilCompleted();
    Ok(())
}
