use std::time::Instant;

pub(super) const CLOCK_SNAP_THRESHOLD: f64 = 0.05;

#[derive(Clone, Copy)]
pub(super) struct Clock {
    base: f64,
    anchor: Option<Instant>,
    rate: f64,
}

impl Clock {
    pub(super) const fn new() -> Self {
        Self {
            base: 0.0,
            anchor: None,
            rate: 1.0,
        }
    }

    pub(super) fn now(&self) -> f64 {
        self.base
            + self
                .anchor
                .map_or(0.0, |a| a.elapsed().as_secs_f64() * self.rate)
    }

    pub(super) fn at(self, time: f64) -> Self {
        Self {
            base: time,
            anchor: self.anchor.map(|_| Instant::now()),
            rate: self.rate,
        }
    }

    pub(super) fn running(self) -> Self {
        Self {
            base: self.base,
            anchor: self.anchor.or_else(|| Some(Instant::now())),
            rate: self.rate,
        }
    }

    pub(super) fn held(self) -> Self {
        Self {
            base: self.now(),
            anchor: None,
            rate: self.rate,
        }
    }

    pub(super) fn with_rate(self, rate: f64) -> Self {
        Self {
            base: self.now(),
            anchor: self.anchor.map(|_| Instant::now()),
            rate,
        }
    }
}
