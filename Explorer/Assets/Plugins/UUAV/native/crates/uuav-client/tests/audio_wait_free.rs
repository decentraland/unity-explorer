#![cfg(target_os = "macos")]
#![allow(clippy::unwrap_used, clippy::expect_used, clippy::indexing_slicing)]

use uuav::uuav_player_read_audio;
use uuav_ipc::audio::{self as jitter, JitterRing, RingState};
use uuav_ipc::protocol::ClockWire;

const SAMPLE_RATE: u32 = 48_000;
const CHANNELS: u32 = 2;
const DSP_FRAMES: usize = 1_024;

fn claim(id: u64) -> &'static JitterRing {
    let ring = jitter::acquire(id).expect("a free slot");
    publish(ring, 0.0);
    ring
}

fn publish(ring: &JitterRing, base: f64) {
    ring.publish_state(RingState {
        clock: ClockWire {
            base,
            anchor_nanos: 0,
            rate: 1.0,
        },
        playing: true,
        sample_rate: SAMPLE_RATE,
        channels: CHANNELS,
    });
}

#[test]
fn the_drain_never_consults_the_registry() {
    let id = 8_103;
    let ring = claim(id);
    ring.push_packet(0.0, &[1.0, 2.0, 3.0, 4.0], false);

    let mut buffer = vec![-1.0f32; DSP_FRAMES * CHANNELS as usize];
    let copied = unsafe { uuav_player_read_audio(id, buffer.as_mut_ptr(), 2) };
    assert_eq!(
        copied, 2,
        "no runtime was ever initialised and no player was ever registered, yet \
         the drain returns this player's samples"
    );
    assert_eq!(&buffer[..4], &[1.0, 2.0, 3.0, 4.0]);
    jitter::release(id);
}
