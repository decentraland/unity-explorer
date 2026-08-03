
use arc_swap::ArcSwapOption;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Duration;

pub(super) const PLAYBACK_POLL: Duration = Duration::from_millis(4);

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

    pub(crate) fn as_flag_ptr(&self) -> *const AtomicBool {
        self.0.as_flag_ptr()
    }
}

impl From<CancelToken> for ReadOnlyCancelToken {
    fn from(c: CancelToken) -> Self {
        Self(c)
    }
}

pub(super) struct AtomicSeekSlot(ArcSwapOption<f64>);

impl AtomicSeekSlot {
    pub(super) fn new() -> Self {
        Self(ArcSwapOption::empty())
    }

    pub(super) fn request(&self, time: f64) {
        self.0.store(Some(Arc::new(time)));
    }

    pub(super) fn take(&self) -> Option<f64> {
        self.0.swap(None).map(|target| *target)
    }

    pub(super) fn is_pending(&self) -> bool {
        self.0.load().is_some()
    }
}
