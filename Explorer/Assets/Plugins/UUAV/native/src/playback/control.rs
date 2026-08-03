
use parking_lot::Mutex;
use std::sync::{Arc, Weak};

struct State<T> {
    value: T,
    pending: bool,
}

type Slot<T> = Mutex<State<T>>;

pub(crate) struct ControlPush<T>(Arc<Slot<T>>);

impl<T: Clone> ControlPush<T> {
    pub(crate) fn new(initial: T) -> Self {
        Self(Arc::new(Mutex::new(State {
            value: initial,
            pending: false,
        })))
    }

    pub(crate) fn push(&self, value: T) {
        let mut state = self.0.lock();
        state.value = value;
        state.pending = true;
    }

    pub(crate) fn latest(&self) -> T {
        self.0.lock().value.clone()
    }

    pub(crate) fn snapshot(&self) -> (T, bool) {
        let state = self.0.lock();
        (state.value.clone(), state.pending)
    }

    pub(crate) fn consumer(&self) -> ControlConsume<T> {
        ControlConsume(Arc::downgrade(&self.0))
    }
}

pub(crate) struct ControlConsume<T>(Weak<Slot<T>>);

impl<T> Clone for ControlConsume<T> {
    fn clone(&self) -> Self {
        Self(self.0.clone())
    }
}

impl<T: Clone> ControlConsume<T> {
    pub(crate) fn consume(&self) -> Option<T> {
        let slot = self.0.upgrade()?;
        let mut state = slot.lock();
        if state.pending {
            state.pending = false;
            Some(state.value.clone())
        } else {
            None
        }
    }

    pub(crate) fn peek(&self) -> Option<T> {
        Some(self.0.upgrade()?.lock().value.clone())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use anyhow::{Context, Result};

    #[test]
    fn consume_takes_once_but_value_persists() -> Result<()> {
        let push = ControlPush::new(1);
        let consume = push.consumer();

        assert_eq!(consume.consume(), None, "nothing pushed yet");
        assert_eq!(consume.peek().context("push half alive")?, 1);

        push.push(2);
        assert_eq!(consume.consume().context("pending update")?, 2);
        assert_eq!(consume.consume(), None, "already consumed");
        assert_eq!(consume.peek().context("value persists")?, 2);
        Ok(())
    }

    #[test]
    fn each_push_is_consumed_exactly_once() -> Result<()> {
        let push = ControlPush::new(0);
        let consume = push.consumer();

        push.push(1);
        assert_eq!(consume.consume().context("first update")?, 1);
        push.push(2);
        assert_eq!(consume.consume().context("second update")?, 2);
        assert_eq!(consume.consume(), None, "no duplicate delivery");
        Ok(())
    }

    #[test]
    fn latest_push_wins() -> Result<()> {
        let push = ControlPush::new(0);
        let consume = push.consumer();

        push.push(1);
        push.push(2);
        assert_eq!(consume.consume().context("pending update")?, 2);
        assert_eq!(push.latest(), 2);
        Ok(())
    }

    #[test]
    fn put_back_hands_the_command_to_the_next_consumer() -> Result<()> {
        let push = ControlPush::new(0);
        let zombie = push.consumer();

        push.push(7);
        assert_eq!(zombie.consume().context("pending update")?, 7);
        Ok(())
    }

    #[test]
    fn snapshot_reports_pending_until_consumed() -> Result<()> {
        let push = ControlPush::new(1);
        let consume = push.consumer();

        assert_eq!(push.snapshot(), (1, false), "nothing pushed yet");

        push.push(2);
        assert_eq!(push.snapshot(), (2, true), "push awaits consumption");

        consume.consume().context("pending update")?;
        assert_eq!(push.snapshot(), (2, false), "consumed, value persists");
        Ok(())
    }

    #[test]
    fn dead_push_half_reads_none() {
        let push = ControlPush::new(1);
        let consume = push.consumer();
        push.push(2);
        drop(push);

        assert_eq!(consume.consume(), None);
        assert_eq!(consume.peek(), None);
    }
}
