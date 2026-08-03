
use crate::audio::JitterRing;
use crate::protocol::{AUDIO_MARKER_CAPACITY, AUDIO_PACKET_SAMPLES, AudioMarker, SharedSegment};

pub struct Producer {
    published: Option<(u32, u32, u64)>,
    carry_discontinuity: bool,
}

impl Default for Producer {
    fn default() -> Self {
        Self::new()
    }
}

impl Producer {
    pub const fn new() -> Self {
        Self {
            published: None,
            carry_discontinuity: false,
        }
    }

    #[allow(
        clippy::too_many_arguments,
        reason = "the adapter's pump threads the same values; a struct would \
                  only rename the coupling"
    )]
    pub fn push_packet(
        &mut self,
        segment: &SharedSegment,
        sample_rate: u32,
        channels: u32,
        options_generation: u64,
        first_pts: f64,
        samples: &[f32],
        discontinuous: bool,
    ) -> usize {
        let discontinuous = discontinuous || self.carry_discontinuity;
        self.carry_discontinuity = false;

        let format = (sample_rate, channels, options_generation);
        if discontinuous || self.published != Some(format) {
            let generation = segment.audio.restart();
            segment
                .audio_format
                .publish(sample_rate, channels, options_generation, generation);
            self.published = Some(format);
        }

        let position = segment.audio.write_position();
        let _accepted = segment.audio.push_marker(AudioMarker {
            position,
            pts: first_pts,
        });

        let pushed = segment.audio.push(samples);
        if pushed < samples.len() {
            self.carry_discontinuity = true;
        }
        pushed
    }
}

const CHUNKS_PER_PUMP: usize = 8;

pub struct Drain {
    generation: u64,
    anchor: Option<AudioMarker>,
    pending: Option<AudioMarker>,
    discontinuous: bool,
    scratch: Box<[f32; AUDIO_PACKET_SAMPLES]>,
}

impl Default for Drain {
    fn default() -> Self {
        Self::new()
    }
}

impl Drain {
    pub fn new() -> Self {
        Self {
            generation: 0,
            anchor: None,
            pending: None,
            discontinuous: true,
            scratch: Box::new([0.0; AUDIO_PACKET_SAMPLES]),
        }
    }

    pub fn pump(&mut self, segment: &SharedSegment, ring: &JitterRing) -> usize {
        let generation = segment.audio.generation();
        if generation != self.generation {
            self.generation = generation;
            self.anchor = None;
            self.pending = None;
            self.discontinuous = true;
        }
        let Some((sample_rate, channels, _options, ring_generation)) = segment.audio_format.read()
        else {
            return 0;
        };
        if ring_generation != generation || sample_rate == 0 || channels == 0 {
            return 0;
        }

        let mut moved = 0usize;
        for _ in 0..CHUNKS_PER_PUMP {
            let read = segment.audio.read_position();

            for _ in 0..AUDIO_MARKER_CAPACITY {
                let marker = match self.pending.take() {
                    Some(marker) => marker,
                    None => match segment.audio.take_marker() {
                        Ok(Some(marker)) => marker,
                        Ok(None) | Err(_) => break,
                    },
                };
                if marker.position <= read {
                    self.anchor = Some(marker);
                } else {
                    self.pending = Some(marker);
                    break;
                }
            }

            let Ok(available) = segment.audio.occupied() else {
                break;
            };
            if available == 0 {
                break;
            }
            let boundary = match self.pending {
                Some(marker) => {
                    usize::try_from(marker.position.wrapping_sub(read)).unwrap_or(available)
                }
                None => available,
            };
            let limit = boundary.min(available).min(AUDIO_PACKET_SAMPLES);
            if limit == 0 {
                continue;
            }
            let Some(chunk) = self.scratch.get_mut(..limit) else {
                break;
            };
            let Ok(popped) = segment.audio.pop_into(chunk) else {
                break;
            };
            if popped == 0 {
                break;
            }
            let Some(pts) = timestamp(self.anchor, read, sample_rate, channels) else {
                self.discontinuous = true;
                continue;
            };
            let Some(chunk) = self.scratch.get(..popped) else {
                break;
            };
            ring.push_packet(pts, chunk, self.discontinuous);
            self.discontinuous = false;
            moved = moved.wrapping_add(1);
        }
        moved
    }
}

fn timestamp(anchor: Option<AudioMarker>, read: u64, sample_rate: u32, channels: u32) -> Option<f64> {
    let anchor = anchor?;
    if sample_rate == 0 {
        return None;
    }
    let frames = read
        .wrapping_sub(anchor.position)
        .checked_div(u64::from(channels))?;
    Some(anchor.pts + frames as f64 / f64::from(sample_rate))
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Admission {
    Admit,
    SkipAndWarn,
    Skip,
}

pub struct DeepFrameGate {
    warned: bool,
}

impl Default for DeepFrameGate {
    fn default() -> Self {
        Self::new()
    }
}

impl DeepFrameGate {
    pub const fn new() -> Self {
        Self { warned: false }
    }

    pub const fn admit(&mut self, bit_depth: u32) -> Admission {
        if bit_depth <= 8 {
            return Admission::Admit;
        }
        if self.warned {
            return Admission::Skip;
        }
        self.warned = true;
        Admission::SkipAndWarn
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
    use crate::audio as jitter;
    use crate::protocol::AUDIO_SAMPLE_CAPACITY;

    fn claim(id: u64) -> &'static JitterRing {
        jitter::acquire(id).expect("a free jitter slot")
    }

    #[test]
    fn a_packet_reaches_the_jitter_ring_with_its_timestamp() {
        let segment = SharedSegment::boxed_zeroed();
        let ring = claim(8_801);
        let mut producer = Producer::new();
        let mut drain = Drain::new();

        let samples: Vec<f32> = (0..16).map(|i| i as f32).collect();
        assert_eq!(
            producer.push_packet(&segment, 48_000, 2, 1, 1.5, &samples, false),
            16
        );
        let generation = segment.audio.generation();
        assert!(generation >= 1);
        assert_eq!(
            segment.audio_format.read(),
            Some((48_000, 2, 1, generation))
        );

        assert_eq!(drain.pump(&segment, ring), 1);
        assert_eq!(segment.audio.occupied().unwrap(), 0, "the segment drained");
        assert_eq!(ring.occupied(), 16);
        let marker = ring.take_marker().expect("the chunk carried its anchor");
        assert_eq!(marker.pts, 1.5);
        let mut out = [0.0f32; 16];
        assert_eq!(ring.pop_into(&mut out), 16);
        assert_eq!(out, *samples.as_slice());
        jitter::release(8_801);
    }

    #[test]
    fn chunk_boundaries_follow_the_producer_packets() {
        let segment = SharedSegment::boxed_zeroed();
        let ring = claim(8_802);
        let mut producer = Producer::new();
        let mut drain = Drain::new();

        let first: Vec<f32> = (0..8).map(|i| i as f32).collect();
        let second: Vec<f32> = (100..106).map(|i| i as f32).collect();
        producer.push_packet(&segment, 48_000, 2, 1, 0.0, &first, false);
        producer.push_packet(&segment, 48_000, 2, 1, 7.25, &second, false);

        assert_eq!(drain.pump(&segment, ring), 2);
        let a = ring.take_marker().unwrap();
        let b = ring.take_marker().unwrap();
        assert_eq!(a.pts, 0.0);
        assert_eq!(b.pts, 7.25, "the second packet keeps its own exact stamp");
        assert_eq!(b.position - a.position, 8, "the boundary is the packet's");
        assert_eq!(ring.occupied(), 14);
        jitter::release(8_802);
    }

    #[test]
    fn a_discontinuous_packet_restarts_both_generations() {
        let segment = SharedSegment::boxed_zeroed();
        let ring = claim(8_803);
        let mut producer = Producer::new();
        let mut drain = Drain::new();

        producer.push_packet(&segment, 48_000, 2, 1, 0.0, &[1.0, 2.0, 3.0, 4.0], false);
        assert_eq!(drain.pump(&segment, ring), 1);
        let segment_generation = segment.audio.generation();
        let jitter_generation = ring.generation();

        producer.push_packet(&segment, 48_000, 2, 1, 90.0, &[9.0, 9.5], true);
        assert!(segment.audio.generation() > segment_generation);
        assert_eq!(drain.pump(&segment, ring), 1);
        assert!(
            ring.generation() > jitter_generation,
            "the jitter consumer must re-anchor after a seek"
        );
        let marker = ring.take_marker().unwrap();
        assert_eq!(marker.pts, 90.0);
        jitter::release(8_803);
    }

    #[test]
    fn a_format_change_restarts_and_republishes() {
        let segment = SharedSegment::boxed_zeroed();
        let ring = claim(8_804);
        let mut producer = Producer::new();
        let mut drain = Drain::new();

        producer.push_packet(&segment, 48_000, 2, 1, 0.0, &[1.0, 2.0], false);
        assert_eq!(drain.pump(&segment, ring), 1);

        producer.push_packet(&segment, 48_000, 6, 2, 0.0, &[0.0; 6], false);
        let generation = segment.audio.generation();
        assert_eq!(
            segment.audio_format.read(),
            Some((48_000, 6, 2, generation)),
            "the ring format follows the engine format it now carries"
        );
        assert_eq!(drain.pump(&segment, ring), 1);
        jitter::release(8_804);
    }

    #[test]
    fn a_full_segment_ring_drops_and_flags_the_next_packet() {
        let segment = SharedSegment::boxed_zeroed();
        let mut producer = Producer::new();

        let flood = vec![0.5f32; AUDIO_SAMPLE_CAPACITY + 7];
        let pushed = producer.push_packet(&segment, 48_000, 2, 1, 0.0, &flood, false);
        assert_eq!(pushed, AUDIO_SAMPLE_CAPACITY, "the overflow is dropped");
        let generation = segment.audio.generation();

        producer.push_packet(&segment, 48_000, 2, 1, 1.0, &[1.0, 2.0], false);
        assert!(
            segment.audio.generation() > generation,
            "the producer re-anchors after a drop"
        );
    }

    #[test]
    fn a_lost_marker_extrapolates_from_the_previous_anchor() {
        let segment = SharedSegment::boxed_zeroed();
        let ring = claim(8_805);
        let mut producer = Producer::new();
        let mut drain = Drain::new();

        let packet: Vec<f32> = (0..1024).map(|i| i as f32).collect();
        producer.push_packet(&segment, 48_000, 2, 1, 1.0, &packet, false);
        segment.audio.push(&[7.0f32; 512]);

        assert_eq!(drain.pump(&segment, ring), 2);
        let first = ring.take_marker().unwrap();
        let second = ring.take_marker().unwrap();
        assert_eq!(first.pts, 1.0);
        assert!((second.pts - (1.0 + 512.0 / 48_000.0)).abs() < 1e-12);
        jitter::release(8_805);
    }

    #[test]
    fn samples_with_no_anchor_at_all_are_dropped_not_mistimed() {
        let segment = SharedSegment::boxed_zeroed();
        let ring = claim(8_806);
        let mut drain = Drain::new();

        let generation = segment.audio.restart();
        segment.audio_format.publish(48_000, 2, 1, generation);
        segment.audio.push(&[1.0f32; 64]);

        assert_eq!(drain.pump(&segment, ring), 0);
        assert_eq!(ring.occupied(), 0, "untimed audio is not pushed");
        assert_eq!(
            segment.audio.occupied().unwrap(),
            0,
            "but the segment does not back up behind it"
        );
        jitter::release(8_806);
    }

    #[test]
    fn the_drain_holds_while_the_format_lags_the_generation() {
        let segment = SharedSegment::boxed_zeroed();
        let ring = claim(8_807);
        let mut producer = Producer::new();
        let mut drain = Drain::new();

        producer.push_packet(&segment, 48_000, 2, 1, 0.0, &[1.0, 2.0], false);
        segment.audio.restart();
        segment.audio.push(&[3.0f32; 8]);

        assert_eq!(drain.pump(&segment, ring), 0, "mid-restart: hold");
        assert_eq!(ring.occupied(), 0);
        jitter::release(8_807);
    }

    #[test]
    fn the_deep_frame_gate_warns_once_and_recovers() {
        let mut gate = DeepFrameGate::new();
        assert_eq!(gate.admit(8), Admission::Admit);
        assert_eq!(gate.admit(10), Admission::SkipAndWarn);
        assert_eq!(gate.admit(10), Admission::Skip, "one warning per session");
        assert_eq!(gate.admit(12), Admission::Skip);
        assert_eq!(
            gate.admit(8),
            Admission::Admit,
            "an 8-bit frame after a deep one still plays: the gate skips \
             frames, never the session"
        );
        assert_eq!(gate.admit(10), Admission::Skip);
        assert_eq!(gate.admit(0), Admission::Admit);
    }
}
