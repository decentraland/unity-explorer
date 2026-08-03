
use anyhow::Result;

use crate::audio::JitterRing;
use crate::win::session::Session;

pub use crate::present_core::{Presented, Presenter, Selection, Stats};

impl Presenter {
    pub const fn new() -> Self {
        Self::with_tolerate_zero_state(true)
    }

    pub fn poll(
        &mut self,
        session: &Session,
        now_nanos: u64,
        audio: Option<&JitterRing>,
    ) -> Result<Selection> {
        self.poll_core(
            session.segment(),
            now_nanos,
            audio,
            |slot| {
                session
                    .surface(slot)
                    .and_then(|surface| surface.geometry().copied())
            },
            |_slot| Ok([0, 0]),
        )
    }
}

impl Default for Presenter {
    fn default() -> Self {
        Self::new()
    }
}
