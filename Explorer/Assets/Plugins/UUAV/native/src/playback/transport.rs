//! Transport state machine: the playback state and the media clock it
//! drives, published as one atomic snapshot.

use arc_swap::ArcSwap;

use super::clock::{CLOCK_SNAP_THRESHOLD, Clock};
use crate::UUAVState;

/// State of a live playback. Mirrors [`UUAVState`] minus the states a
/// playback unit cannot be in: CLOSED, OPENING and ERROR (encoded by the
/// player as the absence of a unit — a unit that exists is open and
/// healthy) and UNKNOWN (fabricated by the FFI layer for missing players).
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

/// State and clock as one value: a transition replaces both at once, so
/// readers can never observe them disagreeing (e.g. ENDED with a running
/// clock).
struct Snapshot {
    state: PlaybackState,
    clock: Clock,
}

/// Transport shared between the engine-facing threads (control, render,
/// audio) and the playback thread, atomic across all of them: transitions
/// swap the whole snapshot; readers never block.
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

    /// Media time at the clock, in seconds.
    pub(super) fn now(&self) -> f64 {
        self.0.load().clock.now()
    }

    pub(super) fn is_playing(&self) -> bool {
        matches!(self.state(), PlaybackState::Playing)
    }

    /// Starts the clock and flags PLAYING.
    pub(super) fn play(&self) {
        self.0.rcu(|s| Snapshot {
            state: PlaybackState::Playing,
            clock: s.clock.running(),
        });
    }

    /// Holds the clock and flags PAUSED.
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

    /// Fully consumed: holds the clock and flags ENDED.
    pub(super) fn ended(&self) {
        self.0.rcu(|s| Snapshot {
            state: PlaybackState::Ended,
            clock: s.clock.held(),
        });
    }

    /// Changes the clock rate in place, preserving the state and the
    /// current media time.
    pub(super) fn set_rate(&self, rate: f64) {
        self.0.rcu(|s| Snapshot {
            state: s.state,
            clock: s.clock.with_rate(rate),
        });
    }

    /// A seek was applied: re-anchors the clock at `target`. A seek out of
    /// ENDED lands paused; `play` (which queues the restart seek itself)
    /// flips the state to PLAYING on its own.
    pub(super) fn rebase(&self, target: f64) {
        self.0.rcu(|s| Snapshot {
            state: match s.state {
                PlaybackState::Ended => PlaybackState::Paused,
                state => state,
            },
            clock: s.clock.at(target),
        });
    }

    /// Slaves the clock to the externally provided master clock;
    /// corrections below the snap threshold are ignored to avoid jitter.
    pub(super) fn sync_to_master(&self, current_time: f64) {
        if (self.now() - current_time).abs() > CLOCK_SNAP_THRESHOLD {
            self.0.rcu(|s| Snapshot {
                state: s.state,
                clock: s.clock.at(current_time),
            });
        }
    }
}
