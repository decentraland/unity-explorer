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
pub(super) struct AtomicSeekSlot(ArcSwapOption<f64>);

impl AtomicSeekSlot {
    pub(super) fn new() -> Self {
        Self(ArcSwapOption::empty())
    }

    /// Requests a seek; an unserviced previous request is simply
    /// overwritten.
    pub(super) fn request(&self, time: f64) {
        self.0.store(Some(Arc::new(time)));
    }

    /// [worker] Takes the pending request, leaving the slot empty.
    pub(super) fn take(&self) -> Option<f64> {
        self.0.swap(None).map(|target| *target)
    }

    /// Whether a request is waiting to be serviced.
    pub(super) fn is_pending(&self) -> bool {
        self.0.load().is_some()
    }
}
