//! Client-side player mirrors: the latest state snapshot per player (from
//! which every per-frame getter is served without touching the channel)
//! plus the *desired* state that makes the helper resurrectable — after a
//! helper crash the recovery worker rebuilds every player from its mirror,
//! with no involvement from C#.
//!
//! The public player ids C# holds are allocated here and stay stable across
//! helper restarts; each mirror tracks the current helper-side id (the core
//! allocates its own), and inbound messages are translated back through the
//! helper->public map.

use arc_swap::ArcSwapOption;
use std::sync::Arc;
use std::sync::Mutex;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
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

/// What the player *should* be doing — enough to rebuild it on a fresh
/// helper. Command paths keep this current; while the helper is down,
/// commands are absorbed here instead of failing.
#[derive(Clone)]
pub struct Desired {
    pub url: Option<String>,
    pub want_playing: bool,
    pub looping: bool,
    pub rate: f64,
    /// Where playback should resume: captured from the extrapolated clock
    /// when the helper dies, overwritten by explicit seeks.
    pub resume_time: Option<f64>,
}

impl Default for Desired {
    fn default() -> Self {
        Self {
            url: None,
            want_playing: false,
            looping: false,
            rate: 1.0,
            resume_time: None,
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
    /// The helper-side id behind this public player; 0 while unbound
    /// (helper down, or the recovery worker has not rebuilt it yet).
    pub helper_id: AtomicU64,
    /// Serializes helper-player creation between the recovery worker and
    /// the lazy binding in command paths.
    pub binding: Mutex<()>,
    /// Set when the helper dies: getters report OPENING and the frozen
    /// resume time until the first snapshot from the resurrected helper.
    pub awaiting_snapshot: AtomicBool,
    pub desired: Mutex<Desired>,
}

impl PlayerMirror {
    pub fn new(sample_rate: i32, channels: i32) -> Self {
        Self {
            state: ArcSwapOption::const_empty(),
            media_info: ArcSwapOption::const_empty(),
            last_master_clock: Mutex::new(None),
            audio: crate::audio_ring::AudioRing::new(sample_rate, channels),
            video: Mutex::default(),
            helper_id: AtomicU64::new(0),
            binding: Mutex::new(()),
            awaiting_snapshot: AtomicBool::new(false),
            desired: Mutex::default(),
        }
    }

    pub fn helper_id(&self) -> Option<PlayerId> {
        match self.helper_id.load(Ordering::Acquire) {
            0 => None,
            id => Some(id),
        }
    }

    /// The frozen position getters serve while the helper is down: the
    /// desired resume point, falling back to the last snapshot's
    /// (unextrapolated) media time.
    pub fn frozen_time(&self) -> Option<f64> {
        let desired = self.desired.lock().ok().and_then(|d| d.resume_time);
        desired.or_else(|| self.state.load().as_ref().and_then(|c| c.update.media_time))
    }
}

pub struct Registry {
    /// Public id (what C# holds) -> mirror.
    players: dashmap::DashMap<PlayerId, Arc<PlayerMirror>>,
    /// Helper-side id -> public id, rebuilt on every helper (re)start.
    by_helper: dashmap::DashMap<PlayerId, PlayerId>,
    next_public: AtomicU64,
    /// `PROCESS_DUP_HANDLE` view of the current helper: texture-set
    /// announcements carry helper-local handle values that are pulled out
    /// of its process through this. Swapped whole on every helper
    /// (re)spawn; a stale one (helper died) just fails the pull.
    #[cfg(target_os = "windows")]
    helper_process: ArcSwapOption<crate::sandbox::OwnedHandle>,
}

impl Registry {
    pub fn new() -> Self {
        Self {
            players: dashmap::DashMap::new(),
            by_helper: dashmap::DashMap::new(),
            next_public: AtomicU64::new(1),
            #[cfg(target_os = "windows")]
            helper_process: ArcSwapOption::const_empty(),
        }
    }

    #[cfg(target_os = "windows")]
    pub fn set_helper_process(&self, source: crate::sandbox::OwnedHandle) {
        self.helper_process.store(Some(Arc::new(source)));
    }

    pub fn create(&self, sample_rate: i32, channels: i32) -> (PlayerId, Arc<PlayerMirror>) {
        let public = self.next_public.fetch_add(1, Ordering::Relaxed);
        let mirror = Arc::new(PlayerMirror::new(sample_rate, channels));
        self.players.insert(public, Arc::clone(&mirror));
        (public, mirror)
    }

    pub fn get(&self, public: PlayerId) -> Option<Arc<PlayerMirror>> {
        self.players.get(&public).map(|m| Arc::clone(&m))
    }

    pub fn contains(&self, public: PlayerId) -> bool {
        self.players.contains_key(&public)
    }

    pub fn remove(&self, public: PlayerId) -> Option<Arc<PlayerMirror>> {
        let (_, mirror) = self.players.remove(&public)?;
        if let Some(helper) = mirror.helper_id() {
            self.by_helper.remove(&helper);
        }
        Some(mirror)
    }

    pub fn len(&self) -> usize {
        self.players.len()
    }

    pub fn bind_helper(&self, public: PlayerId, mirror: &PlayerMirror, helper: PlayerId) {
        mirror.helper_id.store(helper, Ordering::Release);
        self.by_helper.insert(helper, public);
    }

    /// Severs every helper binding after the helper died; the mirrors (and
    /// their desired state) survive for the resurrection.
    pub fn unbind_all(&self) {
        self.by_helper.clear();
        for mirror in &self.players {
            mirror.helper_id.store(0, Ordering::Release);
        }
    }

    pub fn public_of(&self, helper: PlayerId) -> Option<PlayerId> {
        self.by_helper.get(&helper).map(|public| *public)
    }

    fn by_helper(&self, helper: PlayerId) -> Option<Arc<PlayerMirror>> {
        let public = self.public_of(helper)?;
        self.get(public)
    }

    /// Snapshot of the live players, for the recovery worker and the
    /// audio-format reset loop.
    pub fn snapshot(&self) -> Vec<(PlayerId, Arc<PlayerMirror>)> {
        self.players
            .iter()
            .map(|entry| (*entry.key(), Arc::clone(entry.value())))
            .collect()
    }
}

impl Default for Registry {
    fn default() -> Self {
        Self::new()
    }
}

// ---- inbound routing (helper-side ids on the wire) ----------------------

pub fn apply_state(registry: &Registry, update: StateUpdateWire) {
    if let Some(mirror) = registry.by_helper(update.id) {
        mirror.awaiting_snapshot.store(false, Ordering::Release);
        mirror.state.store(Some(Arc::new(CachedState {
            update,
            arrived: Instant::now(),
        })));
    }
}

pub fn apply_media_info(registry: &Registry, id: PlayerId, info: MediaInfoWire) {
    if let Some(mirror) = registry.by_helper(id) {
        mirror.media_info.store(Some(Arc::new(info)));
    }
}

pub fn apply_audio(registry: &Registry, id: PlayerId, samples: &[f32]) {
    if let Some(mirror) = registry.by_helper(id) {
        mirror.audio.write(samples);
    }
}

#[cfg(target_os = "macos")]
pub fn apply_texture_set(registry: &Registry, id: PlayerId, generation: u32, width: u32, height: u32) {
    if let Some(mirror) = registry.by_helper(id)
        && let Ok(mut video) = mirror.video.lock()
    {
        video.store_texture_set(generation, width, height);
    }
}

#[cfg(target_os = "windows")]
pub fn apply_texture_set(
    registry: &Registry,
    id: PlayerId,
    generation: u32,
    width: u32,
    height: u32,
    handles: &[u64],
) {
    // the values are helper-local; pull them into this process first. A
    // failed pull means the helper died or already retired the generation
    // — dropping the announcement is safe either way: the missing ack
    // keeps the helper on its previous generation, and a live helper
    // re-announces on the next resolution change or respawn.
    let Some(source) = registry.helper_process.load_full() else {
        return;
    };
    let Ok(handles) = crate::sandbox::pull_handles(&source, handles) else {
        return;
    };
    if let Some(mirror) = registry.by_helper(id)
        && let Ok(mut video) = mirror.video.lock()
    {
        video.store_texture_set(generation, width, height, handles);
        return;
    }
    // no live mirror to own the pulled handles: close them here or they
    // leak in Unity's process (the player was freed mid-announcement)
    crate::present::close_handles(&handles);
}

pub fn apply_frame_published(registry: &Registry, id: PlayerId, generation: u32, slot: u8) {
    if let Some(mirror) = registry.by_helper(id)
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
    if let Some(mirror) = registry.by_helper(tag.player) {
        if let Ok(mut video) = mirror.video.lock() {
            video.store_surface(tag, surface);
        }
    }
}
