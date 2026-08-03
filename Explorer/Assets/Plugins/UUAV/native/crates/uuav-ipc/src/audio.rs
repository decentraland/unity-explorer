
use std::sync::atomic::{AtomicU32, AtomicU64, Ordering};

use uuav_abi::PlayerId;

use crate::protocol::{AUDIO_PACKET_SAMPLES, AudioMarker, ClockWire, SEQLOCK_READ_ATTEMPTS, uptime_nanos};

pub const JITTER_MS: u64 = 200;

pub const PRIME_TARGET_MS: u64 = 150;

pub const PRIME_DEADLINE_NANOS: u64 = 500_000_000;

pub const RING_SAMPLES: usize = 1 << 17;

pub const MARKER_CAPACITY: usize = 64;

pub const MAX_PLAYERS: usize = 16;

const _: () = assert!(RING_SAMPLES.is_power_of_two());
const _: () = assert!(MARKER_CAPACITY.is_power_of_two());
const _: () = assert!(RING_SAMPLES > AUDIO_PACKET_SAMPLES);
const _: () = assert!(RING_SAMPLES as u64 >= 48_000 * 8 * JITTER_MS / 1_000);

const fn slot_of(index: u64, capacity: usize) -> usize {
    (index & (capacity as u64).wrapping_sub(1)) as usize
}

pub fn prime_target_samples(state: &RingState) -> usize {
    u64::from(state.sample_rate)
        .saturating_mul(u64::from(state.channels))
        .saturating_mul(PRIME_TARGET_MS.min(JITTER_MS))
        .checked_div(1_000)
        .unwrap_or(0)
        .min(RING_SAMPLES as u64) as usize
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct AudioBasis {
    pub generation: u64,
    pub pts: f64,
    pub frames_consumed: u64,
    pub sample_rate: u32,
}

pub fn media_time_of(basis: &AudioBasis, frames_consumed: u64, rate: f64) -> f64 {
    if basis.sample_rate == 0 {
        return basis.pts;
    }
    let advanced = frames_consumed.saturating_sub(basis.frames_consumed);
    basis.pts + advanced as f64 * rate / f64::from(basis.sample_rate)
}

#[repr(C)]
struct MarkerSlot {
    position: AtomicU64,
    pts: AtomicU64,
}

impl MarkerSlot {
    const fn zeroed() -> Self {
        Self {
            position: AtomicU64::new(0),
            pts: AtomicU64::new(0),
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct RingState {
    pub clock: ClockWire,
    pub playing: bool,
    pub sample_rate: u32,
    pub channels: u32,
}

#[repr(C, align(128))]
pub struct JitterRing {
    head: AtomicU64,
    tail: AtomicU64,
    marker_head: AtomicU64,
    marker_tail: AtomicU64,
    generation: AtomicU64,
    restart_nanos: AtomicU64,

    state_sequence: AtomicU64,
    state_words: [AtomicU64; STATE_WORDS],

    anchor_position: AtomicU64,
    anchor_pts: AtomicU64,
    anchor_valid: AtomicU64,
    pending_position: AtomicU64,
    pending_pts: AtomicU64,
    pending_valid: AtomicU64,
    seen_generation: AtomicU64,
    primed_generation: AtomicU64,
    basis_sequence: AtomicU64,
    basis_generation: AtomicU64,
    basis_pts: AtomicU64,
    basis_frames: AtomicU64,
    basis_sample_rate: AtomicU64,
    drained_frames: AtomicU64,
    presentation_pts: AtomicU64,
    presentation_valid: AtomicU64,
    cached_state: [AtomicU64; STATE_WORDS],
    cached_state_valid: AtomicU64,

    packets: AtomicU64,
    pushed_samples: AtomicU64,
    trimmed_samples: AtomicU64,
    drained_samples: AtomicU64,
    skipped_samples: AtomicU64,
    silence_calls: AtomicU64,

    samples: [AtomicU32; RING_SAMPLES],
    markers: [MarkerSlot; MARKER_CAPACITY],
}

const STATE_WORDS: usize = 4;

impl JitterRing {
    #[allow(
        clippy::large_stack_arrays,
        reason = "the only call sites are the `SLOTS` static, where this is BSS \
                  and never touches a stack, and `Box::new` in tests"
    )]
    pub(crate) const fn zeroed() -> Self {
        Self {
            head: AtomicU64::new(0),
            tail: AtomicU64::new(0),
            marker_head: AtomicU64::new(0),
            marker_tail: AtomicU64::new(0),
            generation: AtomicU64::new(0),
            restart_nanos: AtomicU64::new(0),
            state_sequence: AtomicU64::new(0),
            state_words: [const { AtomicU64::new(0) }; STATE_WORDS],
            anchor_position: AtomicU64::new(0),
            anchor_pts: AtomicU64::new(0),
            anchor_valid: AtomicU64::new(0),
            pending_position: AtomicU64::new(0),
            pending_pts: AtomicU64::new(0),
            pending_valid: AtomicU64::new(0),
            seen_generation: AtomicU64::new(0),
            primed_generation: AtomicU64::new(0),
            basis_sequence: AtomicU64::new(0),
            basis_generation: AtomicU64::new(0),
            basis_pts: AtomicU64::new(0),
            basis_frames: AtomicU64::new(0),
            basis_sample_rate: AtomicU64::new(0),
            drained_frames: AtomicU64::new(0),
            presentation_pts: AtomicU64::new(0),
            presentation_valid: AtomicU64::new(0),
            cached_state: [const { AtomicU64::new(0) }; STATE_WORDS],
            cached_state_valid: AtomicU64::new(0),
            packets: AtomicU64::new(0),
            pushed_samples: AtomicU64::new(0),
            trimmed_samples: AtomicU64::new(0),
            drained_samples: AtomicU64::new(0),
            skipped_samples: AtomicU64::new(0),
            silence_calls: AtomicU64::new(0),
            samples: [const { AtomicU32::new(0) }; RING_SAMPLES],
            markers: [const { MarkerSlot::zeroed() }; MARKER_CAPACITY],
        }
    }

    pub fn publish_state(&self, state: RingState) {
        let words = [
            state.clock.base.to_bits(),
            state.clock.anchor_nanos,
            state.clock.rate.to_bits(),
            (u64::from(state.sample_rate) << 32)
                | (u64::from(state.channels) << 16)
                | u64::from(state.playing),
        ];
        let sequence = self.state_sequence.load(Ordering::Relaxed);
        self.state_sequence
            .store(sequence.wrapping_add(1), Ordering::Release);
        for (cell, value) in self.state_words.iter().zip(words) {
            cell.store(value, Ordering::Relaxed);
        }
        self.state_sequence
            .store(sequence.wrapping_add(2), Ordering::Release);
    }

    pub fn restart(&self) {
        let head = self.head.load(Ordering::Relaxed);
        self.tail.store(head, Ordering::Release);
        let marker_head = self.marker_head.load(Ordering::Relaxed);
        self.marker_tail.store(marker_head, Ordering::Release);
        self.restart_nanos.store(uptime_nanos(), Ordering::Relaxed);
        let next = self.generation.load(Ordering::Relaxed).wrapping_add(1);
        self.generation.store(next, Ordering::Release);
    }

    pub fn push_packet(&self, first_pts: f64, samples: &[f32], discontinuous: bool) {
        if discontinuous {
            self.restart();
        }
        let head = self.head.load(Ordering::Relaxed);
        self.push_marker(head, first_pts);

        let mut pushed = 0u64;
        for (offset, value) in samples.iter().enumerate() {
            let index = head.wrapping_add(offset as u64);
            if let Some(cell) = self.samples.get(slot_of(index, RING_SAMPLES)) {
                cell.store(value.to_bits(), Ordering::Relaxed);
                pushed = pushed.wrapping_add(1);
            }
        }
        self.head.store(head.wrapping_add(pushed), Ordering::Release);
        self.packets.fetch_add(1, Ordering::Relaxed);
        self.pushed_samples.fetch_add(pushed, Ordering::Relaxed);
        self.trim();
    }

    fn trim(&self) {
        let Some(state) = self.state() else { return };
        let budget = u64::from(state.sample_rate)
            .saturating_mul(u64::from(state.channels))
            .saturating_mul(JITTER_MS)
            .checked_div(1_000)
            .unwrap_or(0)
            .min(RING_SAMPLES as u64);
        if budget == 0 {
            return;
        }
        let head = self.head.load(Ordering::Relaxed);
        let tail = self.tail.load(Ordering::Acquire);
        let buffered = head.wrapping_sub(tail).min(RING_SAMPLES as u64);
        let excess = buffered.saturating_sub(budget);
        if excess == 0 {
            return;
        }
        self.tail
            .store(tail.wrapping_add(excess), Ordering::Release);
        self.trimmed_samples.fetch_add(excess, Ordering::Relaxed);
    }

    fn push_marker(&self, position: u64, pts: f64) {
        let head = self.marker_head.load(Ordering::Relaxed);
        let tail = self.marker_tail.load(Ordering::Acquire);
        if head.wrapping_sub(tail) >= MARKER_CAPACITY as u64 {
            self.marker_tail.store(
                head.wrapping_sub(MARKER_CAPACITY as u64).wrapping_add(1),
                Ordering::Release,
            );
        }
        let Some(slot) = self.markers.get(slot_of(head, MARKER_CAPACITY)) else {
            return;
        };
        slot.position.store(position, Ordering::Relaxed);
        slot.pts.store(pts.to_bits(), Ordering::Relaxed);
        self.marker_head
            .store(head.wrapping_add(1), Ordering::Release);
    }

    pub fn state(&self) -> Option<RingState> {
        let mut attempts = 0u32;
        while attempts < SEQLOCK_READ_ATTEMPTS {
            attempts = attempts.wrapping_add(1);
            let before = self.state_sequence.load(Ordering::Acquire);
            if before == 0 {
                return None;
            }
            if before & 1 != 0 {
                std::hint::spin_loop();
                continue;
            }
            let mut words = [0u64; STATE_WORDS];
            for (slot, cell) in words.iter_mut().zip(self.state_words.iter()) {
                *slot = cell.load(Ordering::Relaxed);
            }
            if self.state_sequence.load(Ordering::Acquire) != before {
                continue;
            }
            for (cell, value) in self.cached_state.iter().zip(words) {
                cell.store(value, Ordering::Relaxed);
            }
            self.cached_state_valid.store(1, Ordering::Relaxed);
            return Some(decode_state(words));
        }
        debug_assert!(
            attempts <= SEQLOCK_READ_ATTEMPTS,
            "the state read must be bounded: an unbounded retry is not wait-free"
        );
        if self.cached_state_valid.load(Ordering::Relaxed) == 0 {
            return None;
        }
        let mut words = [0u64; STATE_WORDS];
        for (slot, cell) in words.iter_mut().zip(self.cached_state.iter()) {
            *slot = cell.load(Ordering::Relaxed);
        }
        Some(decode_state(words))
    }

    pub fn generation(&self) -> u64 {
        self.generation.load(Ordering::Acquire)
    }

    pub fn seen_generation(&self) -> u64 {
        self.seen_generation.load(Ordering::Relaxed)
    }

    pub fn note_generation(&self, generation: u64) {
        self.seen_generation.store(generation, Ordering::Relaxed);
        self.anchor_valid.store(0, Ordering::Relaxed);
        self.pending_valid.store(0, Ordering::Relaxed);
    }

    pub fn prime_gate_open(&self, generation: u64, state: &RingState, now_nanos: u64) -> bool {
        if self.prime_gate_is_open(generation) {
            return true;
        }
        let elapsed = now_nanos.saturating_sub(self.restart_nanos.load(Ordering::Relaxed));
        if self.occupied() >= prime_target_samples(state) || elapsed >= PRIME_DEADLINE_NANOS {
            self.primed_generation.store(generation, Ordering::Relaxed);
            return true;
        }
        false
    }

    pub fn prime_gate_is_open(&self, generation: u64) -> bool {
        self.primed_generation.load(Ordering::Relaxed) == generation
    }

    pub fn basis_matches(&self, generation: u64) -> bool {
        self.basis_sequence.load(Ordering::Relaxed) != 0
            && self.basis_generation.load(Ordering::Relaxed) == generation
    }

    pub fn capture_basis(&self, basis: AudioBasis) {
        let sequence = self.basis_sequence.load(Ordering::Relaxed);
        self.basis_sequence
            .store(sequence.wrapping_add(1), Ordering::Release);
        self.basis_generation
            .store(basis.generation, Ordering::Relaxed);
        self.basis_pts.store(basis.pts.to_bits(), Ordering::Relaxed);
        self.basis_frames
            .store(basis.frames_consumed, Ordering::Relaxed);
        self.basis_sample_rate
            .store(u64::from(basis.sample_rate), Ordering::Relaxed);
        self.basis_sequence
            .store(sequence.wrapping_add(2), Ordering::Release);
    }

    pub fn basis(&self) -> Option<AudioBasis> {
        let mut attempts = 0u32;
        while attempts < SEQLOCK_READ_ATTEMPTS {
            attempts = attempts.wrapping_add(1);
            let before = self.basis_sequence.load(Ordering::Acquire);
            if before == 0 {
                return None;
            }
            if before & 1 != 0 {
                std::hint::spin_loop();
                continue;
            }
            let basis = AudioBasis {
                generation: self.basis_generation.load(Ordering::Relaxed),
                pts: f64::from_bits(self.basis_pts.load(Ordering::Relaxed)),
                frames_consumed: self.basis_frames.load(Ordering::Relaxed),
                sample_rate: self.basis_sample_rate.load(Ordering::Relaxed) as u32,
            };
            if self.basis_sequence.load(Ordering::Acquire) != before {
                continue;
            }
            return basis.pts.is_finite().then_some(basis);
        }
        None
    }

    pub fn note_drained_frames(&self, frames: u64) {
        self.drained_frames.fetch_add(frames, Ordering::Relaxed);
    }

    pub fn drained_frames(&self) -> u64 {
        self.drained_frames.load(Ordering::Relaxed)
    }

    pub fn set_presentation_clock(&self, media_time: f64) {
        if !media_time.is_finite() {
            return;
        }
        self.presentation_pts
            .store(media_time.to_bits(), Ordering::Relaxed);
        self.presentation_valid.store(1, Ordering::Release);
    }

    pub fn presentation_clock(&self) -> Option<f64> {
        (self.presentation_valid.load(Ordering::Acquire) != 0)
            .then(|| f64::from_bits(self.presentation_pts.load(Ordering::Relaxed)))
    }

    pub fn occupied(&self) -> usize {
        let head = self.head.load(Ordering::Acquire);
        let tail = self.tail.load(Ordering::Relaxed);
        head.wrapping_sub(tail).min(RING_SAMPLES as u64) as usize
    }

    pub fn read_position(&self) -> u64 {
        self.tail.load(Ordering::Relaxed)
    }

    pub fn skip(&self, count: usize) {
        let available = self.occupied().min(count) as u64;
        let tail = self.tail.load(Ordering::Relaxed);
        self.tail
            .store(tail.wrapping_add(available), Ordering::Release);
        self.skipped_samples
            .fetch_add(available, Ordering::Relaxed);
    }

    pub fn pop_into(&self, out: &mut [f32]) -> usize {
        let available = self.occupied().min(out.len());
        let tail = self.tail.load(Ordering::Relaxed);
        let mut copied = 0usize;
        for (offset, slot) in out.iter_mut().take(available).enumerate() {
            let index = tail.wrapping_add(offset as u64);
            if let Some(cell) = self.samples.get(slot_of(index, RING_SAMPLES)) {
                *slot = f32::from_bits(cell.load(Ordering::Relaxed));
                copied = copied.wrapping_add(1);
            }
        }
        self.tail
            .store(tail.wrapping_add(copied as u64), Ordering::Release);
        self.drained_samples
            .fetch_add(copied as u64, Ordering::Relaxed);
        copied
    }

    pub fn take_marker(&self) -> Option<AudioMarker> {
        let head = self.marker_head.load(Ordering::Acquire);
        let tail = self.marker_tail.load(Ordering::Relaxed);
        if head == tail {
            return None;
        }
        let slot = self.markers.get(slot_of(tail, MARKER_CAPACITY))?;
        let marker = AudioMarker {
            position: slot.position.load(Ordering::Relaxed),
            pts: f64::from_bits(slot.pts.load(Ordering::Relaxed)),
        };
        self.marker_tail
            .store(tail.wrapping_add(1), Ordering::Release);
        marker.pts.is_finite().then_some(marker)
    }

    pub fn anchor(&self) -> Option<AudioMarker> {
        (self.anchor_valid.load(Ordering::Relaxed) != 0).then(|| AudioMarker {
            position: self.anchor_position.load(Ordering::Relaxed),
            pts: f64::from_bits(self.anchor_pts.load(Ordering::Relaxed)),
        })
    }

    pub fn set_anchor(&self, marker: AudioMarker) {
        self.anchor_position.store(marker.position, Ordering::Relaxed);
        self.anchor_pts
            .store(marker.pts.to_bits(), Ordering::Relaxed);
        self.anchor_valid.store(1, Ordering::Relaxed);
    }

    pub fn pending(&self) -> Option<AudioMarker> {
        (self.pending_valid.load(Ordering::Relaxed) != 0).then(|| AudioMarker {
            position: self.pending_position.load(Ordering::Relaxed),
            pts: f64::from_bits(self.pending_pts.load(Ordering::Relaxed)),
        })
    }

    pub fn set_pending(&self, marker: Option<AudioMarker>) {
        match marker {
            Some(marker) => {
                self.pending_position
                    .store(marker.position, Ordering::Relaxed);
                self.pending_pts
                    .store(marker.pts.to_bits(), Ordering::Relaxed);
                self.pending_valid.store(1, Ordering::Relaxed);
            }
            None => self.pending_valid.store(0, Ordering::Relaxed),
        }
    }

    pub fn note_silence(&self) {
        self.silence_calls.fetch_add(1, Ordering::Relaxed);
    }

    pub fn counters(&self) -> [u64; 6] {
        [
            self.packets.load(Ordering::Relaxed),
            self.pushed_samples.load(Ordering::Relaxed),
            self.trimmed_samples.load(Ordering::Relaxed),
            self.drained_samples.load(Ordering::Relaxed),
            self.skipped_samples.load(Ordering::Relaxed),
            self.silence_calls.load(Ordering::Relaxed),
        ]
    }

    fn clear(&self) {
        self.head.store(0, Ordering::Relaxed);
        self.tail.store(0, Ordering::Relaxed);
        self.marker_head.store(0, Ordering::Relaxed);
        self.marker_tail.store(0, Ordering::Relaxed);
        self.generation.store(0, Ordering::Relaxed);
        self.restart_nanos.store(0, Ordering::Relaxed);
        self.state_sequence.store(0, Ordering::Relaxed);
        self.anchor_valid.store(0, Ordering::Relaxed);
        self.pending_valid.store(0, Ordering::Relaxed);
        self.seen_generation.store(0, Ordering::Relaxed);
        self.primed_generation.store(0, Ordering::Relaxed);
        self.basis_sequence.store(0, Ordering::Relaxed);
        self.basis_generation.store(0, Ordering::Relaxed);
        self.basis_pts.store(0, Ordering::Relaxed);
        self.basis_frames.store(0, Ordering::Relaxed);
        self.basis_sample_rate.store(0, Ordering::Relaxed);
        self.drained_frames.store(0, Ordering::Relaxed);
        self.presentation_pts.store(0, Ordering::Relaxed);
        self.presentation_valid.store(0, Ordering::Relaxed);
        self.cached_state_valid.store(0, Ordering::Relaxed);
        self.packets.store(0, Ordering::Relaxed);
        self.pushed_samples.store(0, Ordering::Relaxed);
        self.trimmed_samples.store(0, Ordering::Relaxed);
        self.drained_samples.store(0, Ordering::Relaxed);
        self.skipped_samples.store(0, Ordering::Relaxed);
        self.silence_calls.store(0, Ordering::Relaxed);
    }
}

fn decode_state(words: [u64; STATE_WORDS]) -> RingState {
    let [base_bits, anchor_nanos, rate_bits, packed] = words;
    let base = f64::from_bits(base_bits);
    let rate = f64::from_bits(rate_bits);
    RingState {
        clock: ClockWire {
            base: if base.is_finite() { base } else { 0.0 },
            anchor_nanos,
            rate: if rate.is_finite() && rate > 0.0 { rate } else { 1.0 },
        },
        playing: packed & 1 != 0,
        sample_rate: (packed >> 32) as u32,
        channels: ((packed >> 16) & 0xffff) as u32,
    }
}

#[repr(C, align(128))]
struct Slot {
    id: AtomicU64,
    ring: JitterRing,
}

impl Slot {
    const fn zeroed() -> Self {
        Self {
            id: AtomicU64::new(FREE),
            ring: JitterRing::zeroed(),
        }
    }
}

const FREE: u64 = 0;

static SLOTS: [Slot; MAX_PLAYERS] = [const { Slot::zeroed() }; MAX_PLAYERS];

pub fn acquire(id: PlayerId) -> Option<&'static JitterRing> {
    if id == FREE {
        return None;
    }
    for slot in &SLOTS {
        if slot
            .id
            .compare_exchange(FREE, id, Ordering::AcqRel, Ordering::Relaxed)
            .is_ok()
        {
            slot.ring.clear();
            return Some(&slot.ring);
        }
    }
    None
}

pub fn release(id: PlayerId) {
    if id == FREE {
        return;
    }
    for slot in &SLOTS {
        if slot.id.load(Ordering::Acquire) == id {
            slot.ring.clear();
            slot.id.store(FREE, Ordering::Release);
            return;
        }
    }
}

pub fn lookup(id: PlayerId) -> Option<&'static JitterRing> {
    if id == FREE {
        return None;
    }
    let mut visited = 0usize;
    for slot in &SLOTS {
        visited = visited.wrapping_add(1);
        if slot.id.load(Ordering::Acquire) == id {
            return Some(&slot.ring);
        }
    }
    debug_assert!(
        visited <= MAX_PLAYERS,
        "the slot scan must be bounded: an unbounded lookup is not wait-free"
    );
    None
}

#[cfg(test)]
#[allow(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    clippy::float_cmp,
    reason = "test bodies read better with the sharp tools; the crate-wide deny \
              exists to keep them out of the shipped code paths"
)]
mod tests {
    use super::*;

    fn playing_state(sample_rate: u32, channels: u32) -> RingState {
        RingState {
            clock: ClockWire::HELD_AT_ZERO,
            playing: true,
            sample_rate,
            channels,
        }
    }

    #[test]
    fn a_packet_round_trips_with_its_marker() {
        let ring = Box::new(JitterRing::zeroed());
        ring.publish_state(playing_state(48_000, 2));
        let samples: Vec<f32> = (0..8).map(|i| i as f32).collect();
        ring.push_packet(1.5, &samples, false);

        assert_eq!(ring.occupied(), 8);
        assert_eq!(
            ring.take_marker(),
            Some(AudioMarker {
                position: 0,
                pts: 1.5
            })
        );
        let mut out = [-1.0f32; 8];
        assert_eq!(ring.pop_into(&mut out), 8);
        assert_eq!(out, [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0]);
        assert_eq!(ring.occupied(), 0);
    }

    #[test]
    fn the_ring_is_trimmed_to_the_jitter_budget() {
        let ring = Box::new(JitterRing::zeroed());
        ring.publish_state(playing_state(48_000, 2));
        let budget = 48_000 * 2 * JITTER_MS as usize / 1_000;
        let packet: Vec<f32> = vec![0.25; 1024];
        for _ in 0..32 {
            ring.push_packet(0.0, &packet, false);
        }
        assert_eq!(
            ring.occupied(),
            budget,
            "held depth is capped at the jitter budget"
        );
        assert!(
            ring.counters()[2] > 0,
            "the samples past the budget are counted as trimmed"
        );
    }

    #[test]
    fn a_generation_bump_drops_the_anchor() {
        let ring = Box::new(JitterRing::zeroed());
        ring.publish_state(playing_state(48_000, 2));
        ring.push_packet(0.0, &[1.0, 2.0], false);
        ring.set_anchor(AudioMarker {
            position: 0,
            pts: 0.0,
        });
        assert!(ring.anchor().is_some());

        ring.push_packet(90.0, &[3.0, 4.0], true);
        assert_ne!(ring.generation(), ring.seen_generation());
        ring.note_generation(ring.generation());
        assert!(ring.anchor().is_none(), "the stale anchor is dropped");
        assert_eq!(ring.occupied(), 2, "only the new packet survives");
    }

    #[test]
    fn the_wire_wraps_without_losing_alignment() {
        let ring = Box::new(JitterRing::zeroed());
        ring.publish_state(playing_state(48_000, 2));
        ring.head.store(RING_SAMPLES as u64 - 3, Ordering::Release);
        ring.tail.store(RING_SAMPLES as u64 - 3, Ordering::Release);
        let input: Vec<f32> = (0..8).map(|i| i as f32).collect();
        ring.push_packet(0.0, &input, false);
        let mut out = [-1.0f32; 8];
        assert_eq!(ring.pop_into(&mut out), 8);
        assert_eq!(out, [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0]);
    }

    #[test]
    fn the_state_survives_a_seqlock_round_trip() {
        let ring = Box::new(JitterRing::zeroed());
        assert_eq!(ring.state(), None, "nothing published yet");
        let state = RingState {
            clock: ClockWire {
                base: 12.5,
                anchor_nanos: 777,
                rate: 2.0,
            },
            playing: true,
            sample_rate: 44_100,
            channels: 6,
        };
        ring.publish_state(state);
        assert_eq!(ring.state(), Some(state));
    }

    #[test]
    fn the_prime_gate_holds_a_restarted_ring_until_the_target_refill() {
        let ring = Box::new(JitterRing::zeroed());
        let state = playing_state(48_000, 2);
        ring.publish_state(state);
        let target = prime_target_samples(&state);
        assert_eq!(target, 14_400);

        assert!(ring.prime_gate_open(ring.generation(), &state, uptime_nanos()));

        ring.restart();
        let generation = ring.generation();
        let now = uptime_nanos();
        assert!(
            !ring.prime_gate_open(generation, &state, now),
            "empty after the restart: the gate must hold"
        );

        ring.push_packet(5.0, &vec![0.5; target - 2], false);
        assert!(
            !ring.prime_gate_open(generation, &state, now),
            "one sample short of the target is still priming"
        );

        ring.push_packet(5.02, &[0.5, 0.5], false);
        assert!(ring.prime_gate_open(generation, &state, now), "target reached");
        assert!(
            ring.prime_gate_is_open(generation),
            "and it latches open for the generation"
        );
    }

    #[test]
    fn the_prime_gate_gives_up_at_the_deadline() {
        let ring = Box::new(JitterRing::zeroed());
        let state = playing_state(48_000, 2);
        ring.publish_state(state);
        ring.restart();
        let generation = ring.generation();
        ring.push_packet(0.0, &[1.0, 2.0], false);

        let now = uptime_nanos();
        assert!(!ring.prime_gate_open(generation, &state, now));
        assert!(
            ring.prime_gate_open(generation, &state, now.saturating_add(PRIME_DEADLINE_NANOS)),
            "a refill that never comes must not gate forever"
        );
    }

    #[test]
    fn a_second_restart_closes_an_open_gate_again() {
        let ring = Box::new(JitterRing::zeroed());
        let state = playing_state(48_000, 2);
        ring.publish_state(state);
        ring.restart();
        let first = ring.generation();
        ring.push_packet(0.0, &vec![0.1; prime_target_samples(&state)], false);
        assert!(ring.prime_gate_open(first, &state, uptime_nanos()));

        ring.restart();
        let second = ring.generation();
        assert_ne!(first, second);
        assert!(
            !ring.prime_gate_open(second, &state, uptime_nanos()),
            "the new generation must prime from scratch"
        );
    }

    #[test]
    fn the_basis_round_trips_and_tracks_its_generation() {
        let ring = Box::new(JitterRing::zeroed());
        assert_eq!(ring.basis(), None, "no capture yet");
        assert!(!ring.basis_matches(0), "a zero sequence never matches");

        let basis = AudioBasis {
            generation: 3,
            pts: 12.25,
            frames_consumed: 4_800,
            sample_rate: 48_000,
        };
        ring.capture_basis(basis);
        assert_eq!(ring.basis(), Some(basis));
        assert!(ring.basis_matches(3));
        assert!(!ring.basis_matches(4), "a later generation needs a recapture");

        let rebased = AudioBasis {
            generation: 4,
            pts: 0.5,
            frames_consumed: 9_600,
            sample_rate: 48_000,
        };
        ring.capture_basis(rebased);
        assert_eq!(ring.basis(), Some(rebased), "the newest capture wins");
    }

    #[test]
    fn media_time_advances_by_consumed_frames_from_the_basis() {
        let basis = AudioBasis {
            generation: 1,
            pts: 100.0,
            frames_consumed: 1_000,
            sample_rate: 48_000,
        };
        assert_eq!(media_time_of(&basis, 1_000, 1.0), 100.0, "at the basis");
        assert_eq!(media_time_of(&basis, 1_000 + 4_800, 1.0), 100.1);
        assert_eq!(
            media_time_of(&basis, 1_000 + 4_800, 2.0),
            100.2,
            "varispeed scales consumed frames into media seconds"
        );
        assert_eq!(
            media_time_of(&basis, 500, 1.0),
            100.0,
            "a count from before the basis clamps rather than running backwards"
        );
        let unrated = AudioBasis {
            sample_rate: 0,
            ..basis
        };
        assert_eq!(media_time_of(&unrated, 9_999, 1.0), 100.0, "no rate, no advance");
    }

    #[test]
    fn drained_frames_accumulate_and_clear() {
        let ring = Box::new(JitterRing::zeroed());
        assert_eq!(ring.drained_frames(), 0);
        ring.note_drained_frames(480);
        ring.note_drained_frames(20);
        assert_eq!(ring.drained_frames(), 500);
        ring.clear();
        assert_eq!(ring.drained_frames(), 0);
        assert_eq!(ring.basis(), None, "clear drops the basis too");
    }

    #[test]
    fn the_presentation_clock_round_trips_and_clears() {
        let ring = Box::new(JitterRing::zeroed());
        assert_eq!(ring.presentation_clock(), None, "unset before the first publish");
        ring.set_presentation_clock(42.5);
        assert_eq!(ring.presentation_clock(), Some(42.5));
        ring.set_presentation_clock(f64::NAN);
        assert_eq!(
            ring.presentation_clock(),
            Some(42.5),
            "a non-finite time is ignored rather than poisoning the clock"
        );
        ring.clear();
        assert_eq!(ring.presentation_clock(), None, "clear drops it");
    }

    #[test]
    fn an_empty_ring_freezes_the_clock_transiently_then_a_core_refill_resumes_it() {
        fn frame_is_due(pts: f64, now: f64) -> bool {
            !(pts > now)
        }

        let ring = Box::new(JitterRing::zeroed());
        let state = playing_state(48_000, 2);
        ring.publish_state(state);
        let rate = state.clock.rate;

        ring.capture_basis(AudioBasis {
            generation: ring.generation(),
            pts: 100.0,
            frames_consumed: 0,
            sample_rate: 48_000,
        });
        let held_pts = 100.1;

        let publish_clock = |r: &JitterRing| {
            r.set_presentation_clock(media_time_of(&r.basis().unwrap(), r.drained_frames(), rate));
        };
        let mut sink = [0.0f32; 16_384];

        publish_clock(&ring);
        assert_eq!(ring.occupied(), 0, "empty, as after a teleport reopen");
        assert_eq!(ring.presentation_clock(), Some(100.0));
        assert!(
            !frame_is_due(held_pts, ring.presentation_clock().unwrap()),
            "at 100 s the 100.1 s frame is correctly held"
        );

        let frozen = ring.drained_frames();
        let drained = ring.pop_into(&mut sink);
        assert_eq!(drained, 0, "an empty ring yields no frames");
        ring.note_drained_frames(drained as u64 / 2);
        publish_clock(&ring);
        assert_eq!(ring.drained_frames(), frozen, "the clock did not advance while empty");
        assert!(
            !frame_is_due(held_pts, ring.presentation_clock().unwrap()),
            "video is still held - audio and video paused together, no drift"
        );

        ring.push_packet(100.0, &vec![0.0; 150 * 48 * 2], false);
        let refilled = ring.pop_into(&mut sink);
        assert!(refilled > 0, "the refill is drainable");
        ring.note_drained_frames(refilled as u64 / 2);
        publish_clock(&ring);

        let now = ring.presentation_clock().unwrap();
        assert!(now >= held_pts, "the clock resumed past the held pts: {now}");
        assert!(
            frame_is_due(held_pts, now),
            "a core refill - not the clock, not the video path - resumed the present gate"
        );
    }

    #[test]
    fn slots_are_claimed_released_and_reused() {
        let first = acquire(9_001).expect("a free slot");
        assert!(std::ptr::eq(lookup(9_001).expect("registered"), first));
        assert!(lookup(9_002).is_none());

        first.publish_state(playing_state(48_000, 2));
        first.push_packet(0.0, &[1.0, 2.0], false);
        assert_eq!(first.occupied(), 2);

        release(9_001);
        assert!(lookup(9_001).is_none());

        let second = acquire(9_002).expect("the slot comes back");
        assert!(std::ptr::eq(second, first), "the same storage is reused");
        assert_eq!(second.occupied(), 0, "cleared before it is visible again");
        release(9_002);
    }
}
