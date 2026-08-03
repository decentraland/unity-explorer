
use anyhow::{Result, anyhow};

use crate::audio::JitterRing;
use crate::protocol::SharedSegment;

use super::SurfaceTable;

pub use crate::present_core::{Presented, Presenter, Selection, Stats};

impl Presenter {
    pub const fn new() -> Self {
        Self::with_tolerate_zero_state(false)
    }

    pub fn poll(
        &mut self,
        segment: &SharedSegment,
        table: &SurfaceTable,
        now_nanos: u64,
        audio: Option<&JitterRing>,
    ) -> Result<Selection> {
        self.poll_core(
            segment,
            now_nanos,
            audio,
            |slot| table.geometry(slot),
            |slot| {
                table
                    .get(slot)
                    .map(|surface| surface.textures().plane_pointers())
                    .ok_or_else(|| anyhow!("surface slot {slot} vanished mid-poll"))
            },
        )
    }
}

impl Default for Presenter {
    fn default() -> Self {
        Self::new()
    }
}
