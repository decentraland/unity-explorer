
use std::sync::atomic::{AtomicBool, Ordering};

use crate::ControlsState;
use crate::protocol::{PlaybackState, SharedSegment};

pub struct HostControls {
    last_play: AtomicBool,
}

impl HostControls {
    pub const fn new() -> Self {
        Self {
            last_play: AtomicBool::new(false),
        }
    }

    pub fn seek(&self, segment: &SharedSegment, seconds: f64) {
        segment.seek.request(seconds);
    }

    pub fn set_looping(&self, segment: &SharedSegment, looping: bool) {
        segment.controls.set_looping(looping);
    }

    pub fn looping(&self, segment: &SharedSegment) -> bool {
        segment.controls.looping()
    }

    pub fn set_rate(&self, segment: &SharedSegment, rate: f64) {
        segment.controls.request_rate(rate);
    }

    pub fn rate(&self, segment: &SharedSegment) -> f64 {
        segment.controls.requested_rate().0
    }

    pub fn assign_master_clock(&self, segment: &SharedSegment, seconds: f64) {
        segment.controls.request_master_clock(seconds);
    }

    pub fn note_play(&self, play: bool) {
        self.last_play.store(play, Ordering::Release);
    }

    pub fn controls_state(
        &self,
        segment: &SharedSegment,
        transport_state: PlaybackState,
    ) -> ControlsState {
        let looping = segment.controls.looping();
        let (rate, rate_generation) = segment.controls.requested_rate();
        let (applied_looping, applied_rate_generation) = segment.controls_echo.read();

        let play = self.last_play.load(Ordering::Acquire);
        let is_playing = matches!(transport_state, PlaybackState::Playing);

        ControlsState {
            rate,
            play: u8::from(play),
            play_pending: u8::from(play != is_playing),
            looping: u8::from(looping),
            looping_pending: u8::from(looping != applied_looping),
            rate_pending: u8::from(rate_generation != applied_rate_generation),
        }
    }
}

impl Default for HostControls {
    fn default() -> Self {
        Self::new()
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
    use crate::protocol::{PlaybackState, SharedSegment};

    #[test]
    fn seek_coalesces_and_sanitises() {
        let segment = SharedSegment::boxed_zeroed();
        let controls = HostControls::new();

        assert!(!segment.seek.is_pending());
        controls.seek(&segment, 1.0);
        controls.seek(&segment, 3.0);
        assert!(segment.seek.is_pending());
        assert_eq!(segment.seek.take(), Some(3.0), "latest wins");
        assert!(!segment.seek.is_pending());

        controls.seek(&segment, f64::NAN);
        assert_eq!(segment.seek.take(), Some(0.0), "non-finite clamped to zero");
        controls.seek(&segment, -5.0);
        assert_eq!(segment.seek.take(), Some(0.0), "negative clamped to zero");
    }

    #[test]
    fn looping_round_trips() {
        let segment = SharedSegment::boxed_zeroed();
        let controls = HostControls::new();

        assert!(!controls.looping(&segment), "default off");
        controls.set_looping(&segment, true);
        assert!(controls.looping(&segment));
        controls.set_looping(&segment, false);
        assert!(!controls.looping(&segment));
    }

    #[test]
    fn rate_round_trips_and_defaults() {
        let segment = SharedSegment::boxed_zeroed();
        let controls = HostControls::new();

        assert_eq!(controls.rate(&segment), 1.0, "default 1x");
        controls.set_rate(&segment, 2.0);
        assert_eq!(controls.rate(&segment), 2.0);
        controls.set_rate(&segment, 0.0);
        assert_eq!(controls.rate(&segment), 1.0, "zero is invalid -> 1x");
        controls.set_rate(&segment, 100.0);
        assert_eq!(controls.rate(&segment), 1.0, "out of range -> 1x");
        controls.set_rate(&segment, 0.5);
        assert_eq!(controls.rate(&segment), 0.5);
    }

    #[test]
    fn assign_master_clock_bumps_generation() {
        let segment = SharedSegment::boxed_zeroed();
        let controls = HostControls::new();

        assert_eq!(segment.controls.master_clock(), None, "unset");
        controls.assign_master_clock(&segment, 3.5);
        assert_eq!(segment.controls.master_clock(), Some((3.5, 1)));
        controls.assign_master_clock(&segment, 4.0);
        assert_eq!(segment.controls.master_clock(), Some((4.0, 2)), "a new request");
        controls.assign_master_clock(&segment, f64::NAN);
        assert_eq!(
            segment.controls.master_clock(),
            Some((0.0, 3)),
            "non-finite clamped to zero, still a fresh generation"
        );
    }

    #[test]
    fn controls_state_reports_pending_then_clears() {
        let segment = SharedSegment::boxed_zeroed();
        let controls = HostControls::new();

        let state = controls.controls_state(&segment, PlaybackState::Ready);
        assert_eq!((state.play, state.play_pending), (0, 0));
        assert_eq!((state.looping, state.looping_pending), (0, 0));
        assert_eq!(state.rate, 1.0);
        assert_eq!(state.rate_pending, 0);

        controls.note_play(true);
        let state = controls.controls_state(&segment, PlaybackState::Ready);
        assert_eq!((state.play, state.play_pending), (1, 1));
        let state = controls.controls_state(&segment, PlaybackState::Playing);
        assert_eq!((state.play, state.play_pending), (1, 0));

        controls.set_rate(&segment, 2.0);
        let state = controls.controls_state(&segment, PlaybackState::Playing);
        assert_eq!(state.rate, 2.0);
        assert_eq!(state.rate_pending, 1);
        let (_, rate_generation) = segment.controls.requested_rate();
        segment.controls_echo.publish(false, rate_generation);
        let state = controls.controls_state(&segment, PlaybackState::Playing);
        assert_eq!(state.rate_pending, 0, "helper applied the generation");

        controls.set_looping(&segment, true);
        let state = controls.controls_state(&segment, PlaybackState::Playing);
        assert_eq!((state.looping, state.looping_pending), (1, 1));
        segment.controls_echo.publish(true, rate_generation);
        let state = controls.controls_state(&segment, PlaybackState::Playing);
        assert_eq!(state.looping_pending, 0, "helper applied the looping");
        assert_eq!(state.rate_pending, 0, "rate stayed applied");
    }

    #[test]
    fn pause_is_pending_until_the_transport_stops() {
        let segment = SharedSegment::boxed_zeroed();
        let controls = HostControls::new();

        controls.note_play(true);
        assert_eq!(
            controls.controls_state(&segment, PlaybackState::Playing).play_pending,
            0
        );
        controls.note_play(false);
        let state = controls.controls_state(&segment, PlaybackState::Playing);
        assert_eq!((state.play, state.play_pending), (0, 1));
        let state = controls.controls_state(&segment, PlaybackState::Paused);
        assert_eq!((state.play, state.play_pending), (0, 0));
    }
}
