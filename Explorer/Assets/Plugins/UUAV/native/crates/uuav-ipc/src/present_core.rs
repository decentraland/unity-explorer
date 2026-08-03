
use anyhow::{Result, bail};

use uuav_abi::FrameInfo;

use crate::audio::{JitterRing, PRIME_DEADLINE_NANOS};
use crate::protocol::{
    Fault, FrameFault, RETAINED_FRAMES, SURFACE_SLOT_COUNT, SharedSegment, SurfaceGeometry,
    TransportRead, TransportSnapshot, VIDEO_RING_CAPACITY, ValidFrame, VerifyEntry, assemble,
};

pub const VIDEO_PRIME_FRAMES: u64 = 4;
const _: () = assert!(VIDEO_PRIME_FRAMES <= VIDEO_RING_CAPACITY as u64);

pub const VIDEO_PRIME_DEADLINE_NANOS: u64 = PRIME_DEADLINE_NANOS;

const MAX_AUDIO_LATENCY_S: f64 = 1.0;

const LATENCY_SLEW: f64 = 0.02;

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Presented {
    pub sequence: u64,
    pub slot: usize,
    pub pts: Option<f64>,
    pub info: FrameInfo,
    pub reference: Option<VerifyEntry>,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum Selection {
    Idle,
    NotImported { slot: u32 },
    Ready(Presented),
    Faulted(Fault),
}

struct Window {
    slots: [u64; RETAINED_FRAMES],
    head: usize,
    len: usize,
}

impl Window {
    const fn new() -> Self {
        Self {
            slots: [0; RETAINED_FRAMES],
            head: 0,
            len: 0,
        }
    }

    const fn len(&self) -> usize {
        self.len
    }

    fn push(&mut self, sequence: u64) -> Option<u64> {
        if self.len < RETAINED_FRAMES {
            let index = (self.head.wrapping_add(self.len)) % RETAINED_FRAMES;
            if let Some(cell) = self.slots.get_mut(index) {
                *cell = sequence;
            }
            self.len = self.len.wrapping_add(1);
            return None;
        }
        let evicted = self.slots.get(self.head).copied();
        if let Some(cell) = self.slots.get_mut(self.head) {
            *cell = sequence;
        }
        self.head = self.head.wrapping_add(1) % RETAINED_FRAMES;
        evicted
    }

    #[cfg(not(windows))]
    fn pop_oldest(&mut self) -> Option<u64> {
        if self.len == 0 {
            return None;
        }
        let oldest = self.slots.get(self.head).copied();
        self.head = self.head.wrapping_add(1) % RETAINED_FRAMES;
        self.len = self.len.wrapping_sub(1);
        oldest
    }
}

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct Stats {
    pub presented: u64,
    pub dropped_late: u64,
    pub released: u64,
    pub last_sequence: u64,
}

pub struct Presenter {
    last_sequence: u64,
    cached: Option<TransportSnapshot>,
    window: Window,
    stats: Stats,
    surface_generation: u64,
    last_plane_shape: Option<([u32; 2], [u32; 2])>,
    last_now: f64,
    audio_latency: f64,
    last_audio_played: f64,
    tolerate_zero_state: bool,
    started: bool,
    prime_armed_nanos: Option<u64>,
    drops: Vec<(f64, f64)>,
}

impl Presenter {
    pub const fn with_tolerate_zero_state(tolerate_zero_state: bool) -> Self {
        Self {
            last_sequence: 0,
            cached: None,
            window: Window::new(),
            stats: Stats {
                presented: 0,
                dropped_late: 0,
                released: 0,
                last_sequence: 0,
            },
            surface_generation: 0,
            last_plane_shape: None,
            last_now: f64::NAN,
            audio_latency: f64::NAN,
            last_audio_played: f64::NEG_INFINITY,
            tolerate_zero_state,
            started: false,
            prime_armed_nanos: None,
            drops: Vec::new(),
        }
    }

    pub const fn stats(&self) -> Stats {
        self.stats
    }

    pub fn take_drops(&mut self) -> Vec<(f64, f64)> {
        std::mem::take(&mut self.drops)
    }

    pub fn begin_prime(&mut self) {
        self.started = false;
        self.prime_armed_nanos = None;
        self.last_audio_played = f64::NEG_INFINITY;
        self.audio_latency = f64::NAN;
    }

    pub const fn last_now(&self) -> f64 {
        self.last_now
    }

    pub const fn audio_latency(&self) -> f64 {
        self.audio_latency
    }

    pub const fn held(&self) -> usize {
        self.window.len()
    }

    pub fn poll_core<G, P>(
        &mut self,
        segment: &SharedSegment,
        now_nanos: u64,
        audio: Option<&JitterRing>,
        geometry: G,
        planes: P,
    ) -> Result<Selection>
    where
        G: Fn(usize) -> Option<SurfaceGeometry>,
        P: FnOnce(usize) -> Result<[usize; 2]>,
    {
        let snapshot = match segment.transport.read() {
            TransportRead::Fresh(snapshot) => {
                self.cached = Some(snapshot);
                snapshot
            }
            TransportRead::Contended => match self.cached {
                Some(cached) => cached,
                None => return Ok(Selection::Idle),
            },
            TransportRead::Corrupt(Fault::Transport { state: 0 })
                if self.tolerate_zero_state && self.cached.is_none() =>
            {
                return Ok(Selection::Idle);
            }
            TransportRead::Corrupt(fault) => return Ok(Selection::Faulted(fault)),
        };
        let transport = snapshot.clock.now(now_nanos);
        let audio_now = audio.and_then(JitterRing::presentation_clock);
        if let Some(audio_played) = audio_now {
            if audio_played > self.last_audio_played {
                let l_inst = (transport - audio_played).clamp(0.0, MAX_AUDIO_LATENCY_S);
                self.audio_latency = if self.audio_latency.is_nan() {
                    l_inst
                } else {
                    self.audio_latency + LATENCY_SLEW * (l_inst - self.audio_latency)
                };
            }
            self.last_audio_played = audio_played;
        }
        let now = if self.audio_latency.is_nan() {
            transport
        } else {
            transport - self.audio_latency
        };
        self.last_now = now;

        if !self.started {
            match segment.video.depth() {
                Err(fault) => return Ok(Selection::Faulted(fault)),
                Ok(depth) => {
                    let armed = *self.prime_armed_nanos.get_or_insert(now_nanos);
                    let timed_out =
                        now_nanos.saturating_sub(armed) >= VIDEO_PRIME_DEADLINE_NANOS;
                    let audio_started = audio.is_none() || audio_now.is_some();
                    let has_runway = depth >= VIDEO_PRIME_FRAMES && audio_started;
                    if !has_runway && !timed_out {
                        return Ok(Selection::Idle);
                    }
                    self.started = true;
                }
            }
        }

        let mut due: Option<(ValidFrame, Option<VerifyEntry>)> = None;
        let mut blocked_on: Option<u32> = None;

        for _ in 0..VIDEO_RING_CAPACITY {
            let record = match segment.video.peek() {
                Ok(Some(record)) => record,
                Ok(None) => break,
                Err(fault) => return Ok(Selection::Faulted(fault)),
            };

            let slot = record.slot as usize;
            if slot >= SURFACE_SLOT_COUNT {
                return Ok(Selection::Faulted(Fault::Frame(FrameFault::SlotOutOfRange {
                    slot: record.slot,
                })));
            }
            let Some(geometry) = geometry(slot) else {
                blocked_on = Some(record.slot);
                break;
            };

            let valid = match record.validate(self.last_sequence, &geometry) {
                Ok(valid) => valid,
                Err(fault) => return Ok(Selection::Faulted(Fault::Frame(fault))),
            };
            if valid.pts.is_some_and(|pts| pts > now) {
                break;
            }

            let reference = segment.verify.lookup(valid.sequence);
            segment.video.commit();
            self.last_sequence = valid.sequence;
            self.stats.last_sequence = valid.sequence;
            if let Some((superseded, _)) = due.replace((valid, reference)) {
                self.stats.dropped_late = self.stats.dropped_late.wrapping_add(1);
                if let Some(dropped_pts) = superseded.pts
                    && self.drops.len() < 256
                {
                    self.drops.push((dropped_pts, now));
                }
                self.release(segment, superseded.sequence)?;
            }
        }

        let Some((valid, reference)) = due else {
            return Ok(match blocked_on {
                Some(slot) => Selection::NotImported { slot },
                None => Selection::Idle,
            });
        };

        let planes = planes(valid.slot)?;

        let shape = (valid.info.plane_width, valid.info.plane_height);
        if self.last_plane_shape.is_some_and(|previous| previous != shape) {
            self.surface_generation = self.surface_generation.wrapping_add(1);
        }
        self.last_plane_shape = Some(shape);
        self.stats.presented = self.stats.presented.wrapping_add(1);

        let info = assemble(&valid, self.stats.presented, self.surface_generation, planes);

        if let Some(evicted) = self.window.push(valid.sequence) {
            self.release(segment, evicted)?;
        }

        Ok(Selection::Ready(Presented {
            sequence: valid.sequence,
            slot: valid.slot,
            pts: valid.pts,
            info,
            reference,
        }))
    }

    #[cfg(not(windows))]
    pub fn release_all(&mut self, segment: &SharedSegment) -> Result<()> {
        while let Some(sequence) = self.window.pop_oldest() {
            self.release(segment, sequence)?;
        }
        Ok(())
    }

    fn release(&mut self, segment: &SharedSegment, sequence: u64) -> Result<()> {
        if !segment.release.release(sequence) {
            bail!(
                "release ring is full at sequence {sequence}: the host returned more \
                 credit than it took, which RELEASE_RING_CAPACITY is sized to make impossible"
            );
        }
        self.stats.released = self.stats.released.wrapping_add(1);
        Ok(())
    }
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
    use crate::protocol::{
        ClockWire, FRAME_FLAG_HAS_PTS, FrameInfoWire, FrameRecord, PlaybackState,
    };

    const PLANE: (u32, u32) = (64, 48);

    const GEO: SurfaceGeometry = SurfaceGeometry {
        plane_width: [PLANE.0, PLANE.0 / 2],
        plane_height: [PLANE.1, PLANE.1 / 2],
        plane_count: 2,
    };

    struct Fixture {
        segment: Box<SharedSegment>,
        slots: usize,
    }

    fn fixture(slots: usize) -> Fixture {
        Fixture {
            segment: SharedSegment::boxed_zeroed(),
            slots,
        }
    }

    fn primed(tolerate_zero_state: bool) -> Presenter {
        let mut presenter = Presenter::with_tolerate_zero_state(tolerate_zero_state);
        presenter.started = true;
        presenter
    }

    fn poll(presenter: &mut Presenter, fx: &Fixture, now_nanos: u64) -> Result<Selection> {
        let slots = fx.slots;
        presenter.poll_core(
            &fx.segment,
            now_nanos,
            None,
            move |slot| (slot < slots).then_some(GEO),
            |_slot| Ok([1, 2]),
        )
    }

    fn poll_with_audio(
        presenter: &mut Presenter,
        fx: &Fixture,
        ring: &JitterRing,
        now_nanos: u64,
    ) -> Selection {
        let slots = fx.slots;
        presenter
            .poll_core(
                &fx.segment,
                now_nanos,
                Some(ring),
                move |slot| (slot < slots).then_some(GEO),
                |_slot| Ok([1, 2]),
            )
            .unwrap()
    }

    fn record(sequence: u64, slot: u32, pts: f64) -> FrameRecord {
        let (plane_width, plane_height) = PLANE;
        FrameRecord {
            info: FrameInfoWire {
                yuv_to_rgb: [1.0; 12],
                uv_transform: [1.0, 0.0, 0.0, 0.0, -1.0, 1.0],
                visible_width: 64,
                visible_height: 48,
                plane_width: [plane_width, plane_width / 2],
                plane_height: [plane_height, plane_height / 2],
                colorspace: 1,
                color_range: 1,
                color_primaries: 1,
                rotation: 0,
                bit_depth: 8,
            },
            flags: FRAME_FLAG_HAS_PTS,
            pts,
            sequence,
            slot,
            reserved: 0,
        }
    }

    fn hold_clock(segment: &SharedSegment, at: f64) {
        segment.transport.publish(TransportSnapshot {
            state: PlaybackState::Playing,
            clock: ClockWire {
                base: at,
                anchor_nanos: 0,
                rate: 1.0,
            },
        });
    }

    #[test]
    fn a_record_for_an_unimported_slot_is_skipped_without_committing() {
        let fixture = fixture(1);
        hold_clock(&fixture.segment, 10.0);
        assert!(fixture.segment.video.publish(&record(1, 3, 0.0)));

        let mut presenter = primed(false);
        for _ in 0..3 {
            assert_eq!(
                poll(&mut presenter, &fixture, 0).unwrap(),
                Selection::NotImported { slot: 3 }
            );
            assert_eq!(fixture.segment.video.depth().unwrap(), 1);
        }
        assert_eq!(presenter.stats().presented, 0);
    }

    #[test]
    fn a_slot_outside_the_table_is_fatal() {
        let fixture = fixture(1);
        hold_clock(&fixture.segment, 10.0);
        assert!(
            fixture
                .segment
                .video
                .publish(&record(1, SURFACE_SLOT_COUNT as u32, 0.0))
        );
        let mut presenter = primed(false);
        assert!(matches!(
            poll(&mut presenter, &fixture, 0).unwrap(),
            Selection::Faulted(Fault::Frame(FrameFault::SlotOutOfRange { .. }))
        ));
    }

    #[test]
    fn a_plane_larger_than_the_surface_is_fatal() {
        let fixture = fixture(1);
        hold_clock(&fixture.segment, 10.0);
        let mut lying = record(1, 0, 0.0);
        lying.info.plane_width[0] = PLANE.0 + 1;
        assert!(fixture.segment.video.publish(&lying));
        let mut presenter = primed(false);
        assert!(matches!(
            poll(&mut presenter, &fixture, 0).unwrap(),
            Selection::Faulted(Fault::Frame(FrameFault::PlaneExceedsSurface { .. }))
        ));
    }

    #[test]
    fn a_non_advancing_sequence_is_fatal() {
        let fixture = fixture(1);
        hold_clock(&fixture.segment, 10.0);
        let mut presenter = primed(false);
        assert!(fixture.segment.video.publish(&record(5, 0, 0.0)));
        assert!(matches!(
            poll(&mut presenter, &fixture, 0).unwrap(),
            Selection::Ready(_)
        ));
        assert!(fixture.segment.video.publish(&record(5, 0, 0.0)));
        assert!(matches!(
            poll(&mut presenter, &fixture, 0).unwrap(),
            Selection::Faulted(Fault::Frame(FrameFault::SequenceNotAdvancing { .. }))
        ));
    }

    #[test]
    fn late_frames_are_dropped_and_their_credit_returned() {
        let fixture = fixture(2);
        hold_clock(&fixture.segment, 10.0);
        for sequence in 1..=3u64 {
            assert!(fixture.segment.video.publish(&record(
                sequence,
                (sequence % 2) as u32,
                sequence as f64 * 0.01
            )));
        }
        let mut presenter = primed(false);
        let Selection::Ready(shown) = poll(&mut presenter, &fixture, 0).unwrap() else {
            panic!("expected the newest due frame");
        };
        assert_eq!(shown.sequence, 3);
        assert_eq!(presenter.stats().dropped_late, 2);
        assert_eq!(presenter.stats().released, 2);
        assert_eq!(fixture.segment.release.take().unwrap(), Some(1));
        assert_eq!(fixture.segment.release.take().unwrap(), Some(2));
        assert_eq!(fixture.segment.release.take().unwrap(), None);
    }

    #[test]
    fn an_early_frame_is_held_rather_than_presented() {
        let fixture = fixture(1);
        hold_clock(&fixture.segment, 1.0);
        assert!(fixture.segment.video.publish(&record(1, 0, 5.0)));
        let mut presenter = primed(false);
        assert_eq!(poll(&mut presenter, &fixture, 0).unwrap(), Selection::Idle);
        assert_eq!(fixture.segment.video.depth().unwrap(), 1);

        hold_clock(&fixture.segment, 5.0);
        assert!(matches!(
            poll(&mut presenter, &fixture, 0).unwrap(),
            Selection::Ready(_)
        ));
    }

    #[test]
    fn the_retained_window_holds_exactly_four_frames() {
        let fixture = fixture(1);
        let mut presenter = primed(false);
        for sequence in 1..=(RETAINED_FRAMES as u64 + 2) {
            hold_clock(&fixture.segment, sequence as f64);
            assert!(fixture.segment.video.publish(&record(sequence, 0, sequence as f64)));
            let selection = poll(&mut presenter, &fixture, 0).unwrap();
            assert!(matches!(selection, Selection::Ready(_)), "{selection:?}");

            let expected_held = (sequence as usize).min(RETAINED_FRAMES);
            assert_eq!(presenter.held(), expected_held);
            let expected_released = (sequence as usize).saturating_sub(RETAINED_FRAMES);
            assert_eq!(presenter.stats().released as usize, expected_released);
        }
        assert_eq!(fixture.segment.release.take().unwrap(), Some(1));
        assert_eq!(fixture.segment.release.take().unwrap(), Some(2));

        presenter.release_all(&fixture.segment).unwrap();
        for sequence in 3..=(RETAINED_FRAMES as u64 + 2) {
            assert_eq!(fixture.segment.release.take().unwrap(), Some(sequence));
        }
    }

    #[test]
    fn frame_info_indexes_presents_and_tracks_the_surface_generation() {
        let fixture = fixture(1);
        let mut presenter = primed(false);
        let mut generations = Vec::new();
        for sequence in 1..=3u64 {
            hold_clock(&fixture.segment, sequence as f64);
            let mut published = record(sequence, 0, sequence as f64);
            if sequence == 3 {
                published.info.plane_width[0] = PLANE.0 / 2;
                published.info.visible_width = 32;
            }
            assert!(fixture.segment.video.publish(&published));
            let Selection::Ready(shown) = poll(&mut presenter, &fixture, 0).unwrap() else {
                panic!("expected a frame");
            };
            assert_eq!(shown.info.frame_index, sequence);
            assert_ne!(shown.info.planes[0], 0);
            assert_ne!(shown.info.planes[1], 0);
            generations.push(shown.info.surface_generation);
        }
        assert_eq!(generations, vec![0, 0, 1]);
    }

    #[test]
    fn last_now_records_the_scheduling_clock() {
        let fixture = fixture(1);
        let mut presenter = primed(true);
        assert!(presenter.last_now().is_nan());

        hold_clock(&fixture.segment, 7.5);
        assert!(fixture.segment.video.publish(&record(1, 0, 0.0)));
        assert!(matches!(
            presenter
                .poll_core(
                    &fixture.segment,
                    0,
                    None,
                    |slot| (slot < 1).then_some(GEO),
                    |_slot| Ok([0, 0]),
                )
                .unwrap(),
            Selection::Ready(_)
        ));
        assert_eq!(presenter.last_now(), 7.5);
    }

    #[test]
    fn zero_state_is_tolerated_only_when_the_flag_is_set() {
        let win = fixture(1);
        let mut tolerant = primed(true);
        assert_eq!(
            tolerant
                .poll_core(&win.segment, 0, None, |_| Some(GEO), |_| Ok([0, 0]))
                .unwrap(),
            Selection::Idle
        );

        let mac = fixture(1);
        let mut strict = primed(false);
        assert!(matches!(
            strict
                .poll_core(&mac.segment, 0, None, |_| Some(GEO), |_| Ok([0, 0]))
                .unwrap(),
            Selection::Faulted(Fault::Transport { state: 0 })
        ));
    }

    #[test]
    fn zeroed_planes_pass_through_for_the_windows_path() {
        let fixture = fixture(1);
        let mut presenter = primed(true);
        hold_clock(&fixture.segment, 10.0);
        assert!(fixture.segment.video.publish(&record(1, 0, 0.0)));
        let Selection::Ready(shown) = presenter
            .poll_core(
                &fixture.segment,
                0,
                None,
                |slot| (slot < 1).then_some(GEO),
                |_slot| Ok([0, 0]),
            )
            .unwrap()
        else {
            panic!("expected a frame");
        };
        assert_eq!(shown.info.planes, [0, 0]);
    }

    #[test]
    fn an_underfilled_video_ring_holds_the_start() {
        let fixture = fixture(1);
        hold_clock(&fixture.segment, 10.0);
        for sequence in 1..VIDEO_PRIME_FRAMES {
            assert!(fixture.segment.video.publish(&record(sequence, 0, 0.0)));
        }
        let mut presenter = Presenter::with_tolerate_zero_state(false);
        assert_eq!(poll(&mut presenter, &fixture, 0).unwrap(), Selection::Idle);
        assert_eq!(presenter.stats().presented, 0);
        assert_eq!(fixture.segment.video.depth().unwrap(), VIDEO_PRIME_FRAMES - 1);
    }

    #[test]
    fn a_filled_video_ring_releases_the_start() {
        let fixture = fixture(1);
        hold_clock(&fixture.segment, 10.0);
        for sequence in 1..=VIDEO_PRIME_FRAMES {
            assert!(fixture.segment.video.publish(&record(sequence, 0, 0.0)));
        }
        let mut presenter = Presenter::with_tolerate_zero_state(false);
        assert!(matches!(
            poll(&mut presenter, &fixture, 0).unwrap(),
            Selection::Ready(_)
        ));
        assert_eq!(presenter.stats().presented, 1);
    }

    #[test]
    fn the_start_gate_times_out_when_the_ring_never_fills() {
        let fixture = fixture(1);
        hold_clock(&fixture.segment, 10.0);
        assert!(fixture.segment.video.publish(&record(1, 0, 0.0)));
        let mut presenter = Presenter::with_tolerate_zero_state(false);

        assert_eq!(poll(&mut presenter, &fixture, 0).unwrap(), Selection::Idle);
        assert_eq!(presenter.stats().presented, 0);

        assert!(matches!(
            poll(&mut presenter, &fixture, VIDEO_PRIME_DEADLINE_NANOS).unwrap(),
            Selection::Ready(_)
        ));
        assert_eq!(presenter.stats().presented, 1);
    }

    #[test]
    fn begin_prime_re_arms_the_gate_for_a_reopen() {
        let fixture = fixture(1);
        hold_clock(&fixture.segment, 10.0);
        for sequence in 1..=VIDEO_PRIME_FRAMES {
            assert!(fixture.segment.video.publish(&record(sequence, 0, 0.0)));
        }
        let mut presenter = Presenter::with_tolerate_zero_state(false);
        assert!(matches!(
            poll(&mut presenter, &fixture, 0).unwrap(),
            Selection::Ready(_)
        ));

        presenter.begin_prime();
        assert!(fixture.segment.video.publish(&record(VIDEO_PRIME_FRAMES + 1, 0, 0.0)));
        assert_eq!(poll(&mut presenter, &fixture, 0).unwrap(), Selection::Idle);
    }


    #[test]
    fn audio_latency_converges_to_the_mean_residual_despite_noise() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);

        let target = 0.2_f64;
        let noise = [0.05, -0.04, 0.03, -0.06, 0.02, -0.01, 0.04, -0.03];
        let mut audio_played = 0.0_f64;
        for i in 0..4000usize {
            audio_played += 0.01;
            let residual = target + noise[i % noise.len()];
            hold_clock(&fixture.segment, audio_played + residual);
            ring.set_presentation_clock(audio_played);
            assert_eq!(poll_with_audio(&mut presenter, &fixture, &ring, 0), Selection::Idle);
        }
        let l = presenter.audio_latency();
        assert!((l - target).abs() < 0.02, "L={l} should settle near mean {target}");
        let expected_now = audio_played;
        assert!(
            (presenter.last_now() - expected_now).abs() < 0.08,
            "now={} should track audio_played={expected_now}",
            presenter.last_now()
        );
    }

    #[test]
    fn audio_latency_holds_and_now_advances_when_audio_stalls() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);

        let target = 0.2_f64;
        let mut audio_played = 0.0_f64;
        for _ in 0..1000 {
            audio_played += 0.01;
            hold_clock(&fixture.segment, audio_played + target);
            ring.set_presentation_clock(audio_played);
            poll_with_audio(&mut presenter, &fixture, &ring, 0);
        }
        let l_before = presenter.audio_latency();
        let now_before = presenter.last_now();
        assert!((l_before - target).abs() < 1e-3);

        let mut transport = audio_played + target;
        let mut last_now = now_before;
        for _ in 0..500 {
            transport += 0.01;
            hold_clock(&fixture.segment, transport);
            poll_with_audio(&mut presenter, &fixture, &ring, 0);
            let now = presenter.last_now();
            assert!(now > last_now, "now froze during the audio stall: {now} !> {last_now}");
            last_now = now;
        }
        assert!(
            (presenter.audio_latency() - l_before).abs() < 1e-9,
            "L moved while audio was stalled"
        );
        assert!(last_now > now_before + 4.9, "now did not free-run through the stall");
    }

    #[test]
    fn without_audio_now_is_the_bare_transport_clock() {
        let fixture = fixture(1);
        let mut presenter = primed(true);
        hold_clock(&fixture.segment, 12.5);
        assert_eq!(poll(&mut presenter, &fixture, 0).unwrap(), Selection::Idle);
        assert_eq!(presenter.last_now(), 12.5);
        assert!(presenter.audio_latency().is_nan());
    }

    #[test]
    fn before_audio_flows_now_is_the_transport_clock() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);
        hold_clock(&fixture.segment, 9.0);
        assert_eq!(poll_with_audio(&mut presenter, &fixture, &ring, 0), Selection::Idle);
        assert_eq!(presenter.last_now(), 9.0);
        assert!(presenter.audio_latency().is_nan());
    }

    #[test]
    fn audio_latency_is_clamped_to_the_cap() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);
        let mut audio_played = 0.0_f64;
        for _ in 0..500 {
            audio_played += 0.01;
            hold_clock(&fixture.segment, audio_played + 5.0);
            ring.set_presentation_clock(audio_played);
            poll_with_audio(&mut presenter, &fixture, &ring, 0);
        }
        let l = presenter.audio_latency();
        assert!(l <= MAX_AUDIO_LATENCY_S + 1e-9, "L={l} exceeded the cap");
        assert!((l - MAX_AUDIO_LATENCY_S).abs() < 1e-6, "L should saturate at the cap");
    }

    #[test]
    fn audio_latency_never_goes_negative() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);
        let mut audio_played = 0.0_f64;
        for _ in 0..100 {
            audio_played += 0.01;
            hold_clock(&fixture.segment, audio_played - 0.3);
            ring.set_presentation_clock(audio_played);
            poll_with_audio(&mut presenter, &fixture, &ring, 0);
        }
        assert_eq!(presenter.audio_latency(), 0.0);
        assert!(
            (presenter.last_now() - (audio_played - 0.3)).abs() < 1e-9,
            "with L=0, now must equal the bare transport clock"
        );
    }

    fn primed_with_latency(
        presenter: &mut Presenter,
        fx: &Fixture,
        ring: &JitterRing,
        l: f64,
    ) -> f64 {
        let mut audio_played = 0.0_f64;
        for _ in 0..1000 {
            audio_played += 0.01;
            hold_clock(&fx.segment, audio_played + l);
            ring.set_presentation_clock(audio_played);
            poll_with_audio(presenter, fx, ring, 0);
        }
        assert!((presenter.audio_latency() - l).abs() < 1e-3, "L failed to prime near {l}");
        audio_played
    }

    #[test]
    fn a_frame_is_scheduled_against_the_offset_clock() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);

        let l = 0.2_f64;
        let audio_played = primed_with_latency(&mut presenter, &fixture, &ring, l);

        let t = audio_played + l;
        hold_clock(&fixture.segment, t);
        ring.set_presentation_clock(audio_played);
        assert!(fixture.segment.video.publish(&record(1, 0, t - 0.1)));
        assert_eq!(
            poll_with_audio(&mut presenter, &fixture, &ring, 0),
            Selection::Idle,
            "a frame ahead of transport − L must be held, not presented early"
        );

        hold_clock(&fixture.segment, t + 0.15);
        ring.set_presentation_clock(audio_played);
        assert!(
            matches!(
                poll_with_audio(&mut presenter, &fixture, &ring, 0),
                Selection::Ready(_)
            ),
            "once transport − L reaches the frame's pts it presents"
        );
    }

    #[test]
    fn a_backward_audio_seek_holds_l_then_re_anchors() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);

        let l = 0.2_f64;
        let _ = primed_with_latency(&mut presenter, &fixture, &ring, l);
        let l_before = presenter.audio_latency();

        let mut audio_played = 2.0_f64;
        hold_clock(&fixture.segment, audio_played + l);
        ring.set_presentation_clock(audio_played);
        poll_with_audio(&mut presenter, &fixture, &ring, 0);
        assert!(
            (presenter.audio_latency() - l_before).abs() < 1e-9,
            "L moved on the backward seek instead of holding"
        );

        let l2 = 0.1_f64;
        for _ in 0..2000 {
            audio_played += 0.01;
            hold_clock(&fixture.segment, audio_played + l2);
            ring.set_presentation_clock(audio_played);
            poll_with_audio(&mut presenter, &fixture, &ring, 0);
        }
        assert!(
            (presenter.audio_latency() - l2).abs() < 0.01,
            "L did not re-converge to the post-seek latency"
        );
    }

    #[test]
    fn now_free_runs_and_l_stays_bounded_under_flapping_stalls() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);

        let mut audio_played = primed_with_latency(&mut presenter, &fixture, &ring, 0.2);
        let mut transport = audio_played + 0.2;
        let mut last_now = presenter.last_now();

        for cycle in 0..20 {
            for _ in 0..10 {
                transport += 0.01;
                hold_clock(&fixture.segment, transport);
                poll_with_audio(&mut presenter, &fixture, &ring, 0);
                let now = presenter.last_now();
                assert!(now > last_now, "now froze during a stall (cycle {cycle})");
                last_now = now;
                assert!(presenter.audio_latency() <= MAX_AUDIO_LATENCY_S + 1e-9);
            }
            for _ in 0..20 {
                audio_played += 0.01;
                transport += 0.01;
                hold_clock(&fixture.segment, transport);
                ring.set_presentation_clock(audio_played);
                poll_with_audio(&mut presenter, &fixture, &ring, 0);
                let now = presenter.last_now();
                assert!(now > last_now, "now went backward on resume (cycle {cycle})");
                last_now = now;
                assert!(presenter.audio_latency() <= MAX_AUDIO_LATENCY_S + 1e-9);
            }
        }
        let l = presenter.audio_latency();
        assert!(l.is_finite() && (0.0..=MAX_AUDIO_LATENCY_S).contains(&l), "L left its bounds: {l}");
    }

    #[test]
    fn audio_latency_converges_from_an_off_target_seed() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);
        let mut audio_played = 0.0_f64;

        audio_played += 0.01;
        hold_clock(&fixture.segment, audio_played + 0.8);
        ring.set_presentation_clock(audio_played);
        poll_with_audio(&mut presenter, &fixture, &ring, 0);
        assert!((presenter.audio_latency() - 0.8).abs() < 1e-9, "first sample seeds L directly");

        let l = 0.15_f64;
        for _ in 0..3000 {
            audio_played += 0.01;
            hold_clock(&fixture.segment, audio_played + l);
            ring.set_presentation_clock(audio_played);
            poll_with_audio(&mut presenter, &fixture, &ring, 0);
        }
        assert!(
            (presenter.audio_latency() - l).abs() < 0.01,
            "L did not converge down from the off-target seed"
        );
    }

    #[test]
    fn begin_prime_re_anchors_the_audio_latency() {
        let fixture = fixture(1);
        let ring = Box::new(JitterRing::zeroed());
        let mut presenter = primed(true);
        let mut audio_played = 0.0_f64;
        for _ in 0..500 {
            audio_played += 0.01;
            hold_clock(&fixture.segment, audio_played + 0.2);
            ring.set_presentation_clock(audio_played);
            poll_with_audio(&mut presenter, &fixture, &ring, 0);
        }
        assert!((presenter.audio_latency() - 0.2).abs() < 1e-3);

        presenter.begin_prime();
        presenter.started = true;
        assert!(presenter.audio_latency().is_nan(), "reopen must drop the warm L");

        hold_clock(&fixture.segment, 42.0);
        assert_eq!(poll(&mut presenter, &fixture, 0).unwrap(), Selection::Idle);
        assert_eq!(presenter.last_now(), 42.0);
        assert!(presenter.audio_latency().is_nan());
    }
}
