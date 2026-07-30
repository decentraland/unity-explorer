//! Client-side player mirrors: the latest state snapshot per player, from
//! which every per-frame getter is served without touching the channel.

use arc_swap::ArcSwapOption;
use std::sync::Arc;
use std::sync::Mutex;
use std::time::Instant;
use uuav_ipc::protocol::{MediaInfoWire, PlayerId, PlayerStateWire, StateUpdateWire};

pub struct CachedState {
    pub update: StateUpdateWire,
    pub arrived: Instant,
}

impl CachedState {
    /// Media time extrapolated from the snapshot's arrival while playing;
    /// the ~20 ms pump cadence keeps the extrapolation error negligible.
    pub fn media_time_now(&self) -> Option<f64> {
        let media_time = self.update.media_time?;
        if self.update.state == PlayerStateWire::Playing {
            let elapsed = self.arrived.elapsed().as_secs_f64();
            let extrapolated = elapsed.mul_add(self.update.rate, media_time);
            Some(match self.update.duration {
                Some(duration) if !self.update.looping => extrapolated.min(duration),
                _ => extrapolated,
            })
        } else {
            Some(media_time)
        }
    }
}

pub struct PlayerMirror {
    pub state: ArcSwapOption<CachedState>,
    pub media_info: ArcSwapOption<MediaInfoWire>,
    /// Throttles `assign_master_clock` forwarding (called every frame).
    pub last_master_clock: Mutex<Option<Instant>>,
    /// Jitter ring between the helper's audio packets and Unity's audio thread.
    pub audio: crate::audio_ring::AudioRing,
    /// Shared-texture state (assembly + presentation).
    pub video: Mutex<crate::present::PlayerVideo>,
}

impl PlayerMirror {
    pub fn new(sample_rate: i32, channels: i32) -> Self {
        Self {
            state: ArcSwapOption::const_empty(),
            media_info: ArcSwapOption::const_empty(),
            last_master_clock: Mutex::new(None),
            audio: crate::audio_ring::AudioRing::new(sample_rate, channels),
            video: Mutex::default(),
        }
    }
}

pub type Registry = dashmap::DashMap<PlayerId, Arc<PlayerMirror>>;

pub fn apply_state(registry: &Registry, update: StateUpdateWire) {
    if let Some(mirror) = registry.get(&update.id) {
        mirror.state.store(Some(Arc::new(CachedState {
            update,
            arrived: Instant::now(),
        })));
    }
}

pub fn apply_media_info(registry: &Registry, id: PlayerId, info: MediaInfoWire) {
    if let Some(mirror) = registry.get(&id) {
        mirror.media_info.store(Some(Arc::new(info)));
    }
}

pub fn apply_audio(registry: &Registry, id: PlayerId, samples: &[f32]) {
    if let Some(mirror) = registry.get(&id) {
        mirror.audio.write(samples);
    }
}

#[cfg(target_os = "macos")]
pub fn apply_texture_set(registry: &Registry, id: PlayerId, generation: u32, width: u32, height: u32) {
    if let Some(mirror) = registry.get(&id) {
        if let Ok(mut video) = mirror.video.lock() {
            video.store_texture_set(generation, width, height);
        }
    }
}

#[cfg(target_os = "windows")]
pub fn apply_texture_set(
    registry: &Registry,
    id: PlayerId,
    generation: u32,
    width: u32,
    height: u32,
    handles: Vec<u64>,
) {
    if let Some(mirror) = registry.get(&id)
        && let Ok(mut video) = mirror.video.lock()
    {
        video.store_texture_set(generation, width, height, handles);
        return;
    }
    // no live mirror to own the duplicated handles: close them here or they
    // leak in Unity's process (the player was freed mid-announcement)
    crate::present::close_handles(&handles);
}

pub fn apply_frame_published(registry: &Registry, id: PlayerId, generation: u32, slot: u8) {
    if let Some(mirror) = registry.get(&id)
        && let Ok(mut video) = mirror.video.lock()
    {
        video.store_published(generation, slot);
    }
}

#[cfg(target_os = "macos")]
pub fn apply_surface(
    registry: &Registry,
    tag: &uuav_ipc::mach_channel::SurfaceTag,
    surface: objc2_core_foundation::CFRetained<objc2_io_surface::IOSurfaceRef>,
) {
    if let Some(mirror) = registry.get(&tag.player) {
        if let Ok(mut video) = mirror.video.lock() {
            video.store_surface(tag, surface);
        }
    }
}
