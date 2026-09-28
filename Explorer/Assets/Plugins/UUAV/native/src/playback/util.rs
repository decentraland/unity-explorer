//! Commands flowing from the engine-facing threads to the playback
//! thread: shutdown and seek.

use arc_swap::ArcSwapOption;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Duration;

/// Poll interval of the playback thread when it has to wait for queue
/// space or the next command.
pub(super) const PLAYBACK_POLL: Duration = Duration::from_millis(4);

/// Shutdown command: makes the playback thread exit and aborts blocking
/// demuxer I/O. Cloning shares the token.
#[derive(Clone)]
pub(crate) struct CancelToken(Arc<AtomicBool>);

impl CancelToken {
    pub(crate) fn new() -> Self {
        Self(Arc::new(AtomicBool::new(false)))
    }

    pub(crate) fn cancel(&self) {
        self.0.store(true, Ordering::Release);
    }

    pub(crate) fn is_cancelled(&self) -> bool {
        self.0.load(Ordering::Acquire)
    }

    /// Address of the flag for the demuxer's C interrupt callback; valid
    /// for as long as any clone of the token is alive.
    fn as_flag_ptr(&self) -> *const AtomicBool {
        Arc::as_ptr(&self.0)
    }
}

#[derive(Clone)]
pub(crate) struct ReadOnlyCancelToken(CancelToken);

impl ReadOnlyCancelToken {
    pub(crate) fn is_cancelled(&self) -> bool {
        self.0.is_cancelled()
    }

    /// Address of the flag for the demuxer's C interrupt callback; valid
    /// for as long as any clone of the token is alive.
    pub(crate) fn as_flag_ptr(&self) -> *const AtomicBool {
        self.0.as_flag_ptr()
    }
}

impl From<CancelToken> for ReadOnlyCancelToken {
    fn from(c: CancelToken) -> Self {
        Self(c)
    }
}

/// Coalescing seek command, atomic across threads: engine-facing threads
/// overwrite the pending target, the playback thread takes it.
pub(crate) struct AtomicSeekSlot(ArcSwapOption<f64>);

impl AtomicSeekSlot {
    pub(crate) fn new() -> Self {
        Self(ArcSwapOption::empty())
    }

    /// Requests a seek; an unserviced previous request is simply
    /// overwritten.
    pub(crate) fn request(&self, time: f64) {
        self.0.store(Some(Arc::new(time)));
    }

    /// Requests a seek only while nothing is pending; an unserviced
    /// request keeps its target. Returns whether the request was placed.
    pub(crate) fn request_if_empty(&self, time: f64) -> bool {
        let previous = self
            .0
            .compare_and_swap(std::ptr::null::<f64>(), Some(Arc::new(time)));
        previous.is_none()
    }

    /// Drops the pending request, if any: a target requested for a media
    /// that is being closed must not carry over to the next one.
    pub(crate) fn clear(&self) {
        self.0.store(None);
    }

    /// [worker] Takes the pending request, leaving the slot empty.
    pub(crate) fn take(&self) -> Option<f64> {
        self.0.swap(None).map(|target| *target)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn take_empties_the_slot() {
        let slot = AtomicSeekSlot::new();
        assert_eq!(slot.take(), None, "nothing requested yet");

        slot.request(3.0);
        assert_eq!(slot.take(), Some(3.0));
        assert_eq!(slot.take(), None, "already taken");
    }

    #[test]
    fn latest_request_wins() {
        let slot = AtomicSeekSlot::new();
        slot.request(1.0);
        slot.request(2.0);
        assert_eq!(slot.take(), Some(2.0));
    }

    #[test]
    fn request_if_empty_places_into_an_empty_slot() {
        let slot = AtomicSeekSlot::new();
        assert!(slot.request_if_empty(4.0));
        assert_eq!(slot.take(), Some(4.0));
    }

    #[test]
    fn request_if_empty_keeps_the_pending_request() {
        let slot = AtomicSeekSlot::new();
        slot.request(7.0);
        assert!(!slot.request_if_empty(0.0));
        assert_eq!(slot.take(), Some(7.0), "the pending target survives");
    }

    #[test]
    fn clear_drops_the_pending_request() {
        let slot = AtomicSeekSlot::new();
        slot.request(5.0);
        slot.clear();
        assert_eq!(slot.take(), None);
    }
}
