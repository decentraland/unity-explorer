
use arc_swap::ArcSwap;

use super::clock::{CLOCK_SNAP_THRESHOLD, Clock};
use crate::UUAVState;

#[derive(Clone, Copy)]
pub(super) enum PlaybackState {
    Ready,
    Playing,
    Paused,
    Ended,
}

impl From<PlaybackState> for UUAVState {
    fn from(state: PlaybackState) -> Self {
        match state {
            PlaybackState::Ready => Self::UUAV_READY,
            PlaybackState::Playing => Self::UUAV_PLAYING,
            PlaybackState::Paused => Self::UUAV_PAUSED,
            PlaybackState::Ended => Self::UUAV_ENDED,
        }
    }
}

struct Snapshot {
    state: PlaybackState,
    clock: Clock,
}

pub(super) struct AtomicTransport(ArcSwap<Snapshot>);

impl AtomicTransport {
    pub(super) fn new() -> Self {
        Self(ArcSwap::from_pointee(Snapshot {
            state: PlaybackState::Ready,
            clock: Clock::new(),
        }))
    }

    pub(super) fn state(&self) -> PlaybackState {
        self.0.load().state
    }

    pub(super) fn now(&self) -> f64 {
        self.0.load().clock.now()
    }

    pub(super) fn is_playing(&self) -> bool {
        matches!(self.state(), PlaybackState::Playing)
    }

    pub(super) fn play(&self) {
        self.0.rcu(|s| Snapshot {
            state: PlaybackState::Playing,
            clock: s.clock.running(),
        });
    }

    pub(super) fn pause(&self) {
        match self.state() {
            PlaybackState::Playing => {
                self.0.rcu(|s| Snapshot {
                    state: PlaybackState::Paused,
                    clock: s.clock.held(),
                });
            }
            PlaybackState::Ready | PlaybackState::Paused | PlaybackState::Ended => {}
        }
    }

    pub(super) fn ended(&self) {
        self.0.rcu(|s| Snapshot {
            state: PlaybackState::Ended,
            clock: s.clock.held(),
        });
    }

    pub(super) fn set_rate(&self, rate: f64) {
        self.0.rcu(|s| Snapshot {
            state: s.state,
            clock: s.clock.with_rate(rate),
        });
    }

    pub(super) fn rebase(&self, target: f64) {
        self.0.rcu(|s| Snapshot {
            state: match s.state {
                PlaybackState::Ended => PlaybackState::Paused,
                state => state,
            },
            clock: s.clock.at(target),
        });
    }

    pub(super) fn sync_to_master(&self, current_time: f64) {
        if (self.now() - current_time).abs() > CLOCK_SNAP_THRESHOLD {
            self.0.rcu(|s| Snapshot {
                state: s.state,
                clock: s.clock.at(current_time),
            });
        }
    }
}
