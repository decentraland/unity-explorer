
use std::num::NonZeroUsize;

use uuav_abi::PlayerId;
use uuav_ipc::audio::{self as jitter, AudioBasis, JitterRing, MARKER_CAPACITY};
use uuav_ipc::protocol::{AudioMarker, uptime_nanos};

const AUDIO_DRIFT_TOLERANCE: f64 = 0.15;

enum ClockSync {
    Consume,
    EmitSilence,
}

pub unsafe fn read(player_id: PlayerId, dst: *mut f32, frames: usize) -> i32 {
    let Some(ring) = jitter::lookup(player_id) else {
        return 0;
    };
    let Some(state) = ring.state() else {
        return 0;
    };
    let Some(channels) = NonZeroUsize::new(state.channels as usize) else {
        return 0;
    };
    let Some(total) = frames.checked_mul(channels.get()) else {
        return 0;
    };
    let out = unsafe { std::slice::from_raw_parts_mut(dst, total) };

    let generation = ring.generation();
    if generation != ring.seen_generation() {
        ring.note_generation(generation);
    }

    if !state.playing {
        out.fill(0.0);
        ring.note_silence();
        return 0;
    }

    let now_nanos = uptime_nanos();
    if !ring.prime_gate_open(generation, &state, now_nanos) {
        out.fill(0.0);
        ring.note_silence();
        return 0;
    }

    let now = state.clock.now(now_nanos);
    let rate = state.clock.rate;
    match sync_to_clock(ring, now, rate, channels, state.sample_rate) {
        ClockSync::Consume => {
            if !ring.basis_matches(generation)
                && let Some(pts) = next_pts(ring, channels, state.sample_rate, rate)
            {
                ring.capture_basis(AudioBasis {
                    generation,
                    pts,
                    frames_consumed: ring.drained_frames(),
                    sample_rate: state.sample_rate,
                });
            }
            let copied = read_into(ring, out, channels);
            i32::try_from(copied).unwrap_or(0)
        }
        ClockSync::EmitSilence => {
            out.fill(0.0);
            ring.note_silence();
            0
        }
    }
}

fn sync_to_clock(
    ring: &JitterRing,
    now: f64,
    rate: f64,
    channels: NonZeroUsize,
    sample_rate: u32,
) -> ClockSync {
    let Some(pts) = next_pts(ring, channels, sample_rate, rate) else {
        return ClockSync::Consume;
    };
    let drift = pts - now;
    if drift > AUDIO_DRIFT_TOLERANCE {
        return ClockSync::EmitSilence;
    }
    if drift < -AUDIO_DRIFT_TOLERANCE && rate > 0.0 {
        let late_frames = (-drift * f64::from(sample_rate) / rate) as usize;
        let buffered_frames = ring.occupied().checked_div(channels.get()).unwrap_or(0);
        ring.skip(
            late_frames
                .min(buffered_frames)
                .saturating_mul(channels.get()),
        );
    }
    ClockSync::Consume
}

fn next_pts(
    ring: &JitterRing,
    channels: NonZeroUsize,
    sample_rate: u32,
    rate: f64,
) -> Option<f64> {
    let read = ring.read_position();
    let mut steps = 0usize;
    while steps < MARKER_CAPACITY {
        steps = steps.wrapping_add(1);
        let marker = match ring.pending() {
            Some(marker) => {
                ring.set_pending(None);
                marker
            }
            None => match ring.take_marker() {
                Some(marker) => marker,
                None => break,
            },
        };
        if marker.position <= read {
            ring.set_anchor(marker);
        } else {
            ring.set_pending(Some(marker));
            break;
        }
    }
    debug_assert!(
        steps <= MARKER_CAPACITY,
        "the marker walk must be bounded: an unbounded drain is not wait-free"
    );

    if sample_rate == 0 {
        return None;
    }
    ring.anchor().map(|marker: AudioMarker| {
        let samples_past = read.saturating_sub(marker.position) as usize;
        let frames_past = samples_past.checked_div(channels.get()).unwrap_or(0);
        marker.pts + frames_past as f64 * rate / f64::from(sample_rate)
    })
}

fn read_into(ring: &JitterRing, out: &mut [f32], channels: NonZeroUsize) -> usize {
    let requested_frames = out.len().checked_div(channels.get()).unwrap_or(0);
    let buffered_frames = ring.occupied().checked_div(channels.get()).unwrap_or(0);
    let wanted = requested_frames
        .min(buffered_frames)
        .saturating_mul(channels.get());
    let copied = match out.get_mut(..wanted) {
        Some(destination) => ring.pop_into(destination),
        None => 0,
    };
    if let Some(rest) = out.get_mut(copied..) {
        rest.fill(0.0);
    }
    if copied < out.len() {
        ring.note_silence();
    }
    let frames = copied.checked_div(channels.get()).unwrap_or(0);
    ring.note_drained_frames(frames as u64);
    frames
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
    use uuav_ipc::audio::RingState;
    use uuav_ipc::protocol::ClockWire;

    fn claim(id: PlayerId, sample_rate: u32, channels: u32, base: f64) -> &'static JitterRing {
        let ring = jitter::acquire(id).expect("a free slot");
        ring.publish_state(RingState {
            clock: ClockWire {
                base,
                anchor_nanos: 0,
                rate: 1.0,
            },
            playing: true,
            sample_rate,
            channels,
        });
        ring
    }

    #[test]
    fn an_unknown_player_writes_nothing() {
        let mut dst = [7.0f32; 8];
        let copied = unsafe { read(7_701, dst.as_mut_ptr(), 4) };
        assert_eq!(copied, 0);
        assert_eq!(dst, [7.0f32; 8], "an unresolvable player is not written to");
    }

    #[test]
    fn samples_drain_contiguously_across_callbacks() {
        let ring = claim(7_702, 48_000, 2, 0.0);
        let samples: Vec<f32> = (0..16).map(|i| i as f32).collect();
        ring.push_packet(0.0, &samples, false);

        let mut first = [-1.0f32; 8];
        assert_eq!(unsafe { read(7_702, first.as_mut_ptr(), 4) }, 4);
        assert_eq!(first, [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0]);

        let mut second = [-1.0f32; 8];
        assert_eq!(unsafe { read(7_702, second.as_mut_ptr(), 4) }, 4);
        assert_eq!(second, [8.0, 9.0, 10.0, 11.0, 12.0, 13.0, 14.0, 15.0]);
        jitter::release(7_702);
    }

    #[test]
    fn a_paused_player_holds_silence_without_draining() {
        let ring = claim(7_703, 48_000, 2, 0.0);
        ring.push_packet(0.0, &[1.0, 2.0, 3.0, 4.0], false);
        ring.publish_state(RingState {
            clock: ClockWire::HELD_AT_ZERO,
            playing: false,
            sample_rate: 48_000,
            channels: 2,
        });

        let mut dst = [3.0f32; 4];
        assert_eq!(unsafe { read(7_703, dst.as_mut_ptr(), 2) }, 0);
        assert_eq!(dst, [0.0f32; 4]);
        assert_eq!(ring.occupied(), 4, "nothing drained");
        jitter::release(7_703);
    }

    #[test]
    fn samples_ahead_of_the_clock_are_held_back() {
        let ring = claim(7_704, 48_000, 2, 0.0);
        let samples: Vec<f32> = (0..960).map(|i| i as f32).collect();
        ring.push_packet(10.0, &samples, false);

        let mut dst = [5.0f32; 8];
        assert_eq!(unsafe { read(7_704, dst.as_mut_ptr(), 4) }, 0);
        assert_eq!(dst, [0.0f32; 8]);
        assert_eq!(ring.occupied(), 960, "held back, nothing dropped");
        jitter::release(7_704);
    }

    #[test]
    fn late_samples_are_skipped_before_the_copy() {
        let ring = claim(7_705, 48_000, 2, 0.16);
        let samples: Vec<f32> = (0..3_840).map(|i| i as f32).collect();
        ring.push_packet(0.0, &samples, false);

        let mut dst = [-1.0f32; 2_048];
        let copied = unsafe { read(7_705, dst.as_mut_ptr(), 1_024) };
        assert_eq!(copied, 0);
        assert_eq!(ring.occupied(), 0, "the late span was dropped");
        jitter::release(7_705);
    }

    #[test]
    fn a_discontinuous_packet_re_anchors() {
        let ring = claim(7_706, 48_000, 2, 0.0);
        ring.push_packet(0.0, &[1.0, 2.0, 3.0, 4.0], false);
        let mut dst = [-1.0f32; 4];
        assert_eq!(unsafe { read(7_706, dst.as_mut_ptr(), 2) }, 2);

        ring.push_packet(100.0, &[9.0, 9.5, 10.0, 10.5], true);
        ring.publish_state(RingState {
            clock: ClockWire {
                base: 100.0,
                anchor_nanos: 0,
                rate: 1.0,
            },
            playing: true,
            sample_rate: 48_000,
            channels: 2,
        });

        let mut gated = [-1.0f32; 4];
        assert_eq!(unsafe { read(7_706, gated.as_mut_ptr(), 2) }, 0);
        assert_eq!(gated, [0.0f32; 4]);
        assert_eq!(ring.occupied(), 4, "nothing drains while priming");

        let filler = vec![0.25f32; 14_400 - 4];
        ring.push_packet(100.0 + 2.0 / 48_000.0, &filler, false);

        let mut after = [-1.0f32; 4];
        assert_eq!(
            unsafe { read(7_706, after.as_mut_ptr(), 2) },
            2,
            "without the re-anchor the new samples look 100 s late and are all \
             skipped instead"
        );
        assert_eq!(after, [9.0, 9.5, 10.0, 10.5]);

        let basis = ring.basis().expect("the first consume captures a basis");
        assert_eq!(basis.generation, ring.generation());
        assert_eq!(basis.pts, 100.0);
        assert_eq!(basis.sample_rate, 48_000);
        assert_eq!(
            basis.frames_consumed, 2,
            "two frames were drained before the seek"
        );
        jitter::release(7_706);
    }

    #[test]
    fn an_underrun_pads_the_tail_with_silence() {
        let ring = claim(7_707, 48_000, 2, 0.0);
        ring.push_packet(0.0, &[1.0, 2.0], false);

        let mut dst = [-1.0f32; 8];
        assert_eq!(unsafe { read(7_707, dst.as_mut_ptr(), 4) }, 1);
        assert_eq!(dst, [1.0, 2.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]);
        jitter::release(7_707);
    }
}
