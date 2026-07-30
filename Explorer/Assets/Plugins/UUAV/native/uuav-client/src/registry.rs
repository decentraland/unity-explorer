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

#[derive(Default)]
pub struct PlayerMirror {
    pub state: ArcSwapOption<CachedState>,
    pub media_info: ArcSwapOption<MediaInfoWire>,
    /// Throttles `assign_master_clock` forwarding (called every frame).
    pub last_master_clock: Mutex<Option<Instant>>,
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
