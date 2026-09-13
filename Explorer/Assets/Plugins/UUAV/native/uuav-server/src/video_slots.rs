//! Platform-neutral generation/slot bookkeeping for the video pumps:
//! the announce → ack → activate state machine and the slot rotation,
//! generic over what a slot actually is (IOSurfaces, keyed-mutex
//! textures, exported Vulkan images).
//!
//! Extracted for the Linux pump; the macOS/Windows pumps still carry
//! their own copies and adopt this on their next platform-verified
//! change.

use anyhow::Result;

pub const SLOTS: usize = 3;

/// One announced generation: the slots plus the dimensions they were
/// created for.
pub struct GenSlots<S> {
    pub generation: u32,
    pub width: u32,
    pub height: u32,
    pub slots: [S; SLOTS],
    next_slot: usize,
}

impl<S> GenSlots<S> {
    pub const fn matches(&self, width: u32, height: u32) -> bool {
        self.width == width && self.height == height
    }
}

/// Per-player video state, driven once per tick.
pub struct PlayerVideo<S> {
    next_generation: u32,
    /// Announced and acked: the generation frames are published into.
    active: Option<GenSlots<S>>,
    /// Announced, awaiting `TextureSetAck`; `active` keeps publishing.
    pending: Option<GenSlots<S>>,
}

impl<S> Default for PlayerVideo<S> {
    fn default() -> Self {
        Self {
            next_generation: 0,
            active: None,
            pending: None,
        }
    }
}

impl<S> PlayerVideo<S> {
    /// The client wrapped the announced set: switch over (the replaced
    /// slots stay alive through the client's own references until it
    /// drops them).
    pub fn ack(&mut self, generation: u32) {
        if self
            .pending
            .as_ref()
            .is_some_and(|pending| pending.generation == generation)
        {
            self.active = self.pending.take();
        }
    }

    /// Ensures a generation matching the frame size exists, building
    /// and announcing one through `create` when needed. A first
    /// generation activates optimistically (publishes are ignored by
    /// the client until it wraps and acks); a replacement waits in
    /// `pending`.
    pub fn ensure_generation(
        &mut self,
        width: u32,
        height: u32,
        create: impl FnOnce(u32) -> Result<[S; SLOTS]>,
    ) -> Result<()> {
        if self.active.as_ref().is_some_and(|g| g.matches(width, height))
            || self.pending.as_ref().is_some_and(|g| g.matches(width, height))
        {
            return Ok(());
        }
        self.next_generation = self.next_generation.wrapping_add(1);
        let generation = self.next_generation;
        let slots = create(generation)?;
        let created = GenSlots {
            generation,
            width,
            height,
            slots,
            next_slot: 0,
        };
        if self.active.is_none() {
            self.active = Some(created);
        } else {
            self.pending = Some(created);
        }
        Ok(())
    }

    /// The slot to publish this frame into, rotating; `None` while the
    /// active generation is stale (resolution changed, replacement
    /// pending ack) — keep the last published frame instead of writing
    /// stale dimensions into the active slots.
    pub fn take_slot(&mut self, width: u32, height: u32) -> Option<(&S, u32, u8)> {
        let active = self.active.as_mut()?;
        if !active.matches(width, height) {
            return None;
        }
        let slot_index = active.next_slot;
        active.next_slot = slot_index.wrapping_add(1) % SLOTS;
        let slot = active.slots.get(slot_index)?;
        #[allow(clippy::cast_possible_truncation)] // SLOTS = 3
        Some((slot, active.generation, slot_index as u8))
    }
}

#[cfg(test)]
#[allow(clippy::unwrap_used, clippy::expect_used, clippy::panic)]
mod tests {
    use super::*;

    fn make(generation: u32) -> Result<[u32; SLOTS]> {
        Ok([generation * 10, generation * 10 + 1, generation * 10 + 2])
    }

    #[test]
    fn first_generation_activates_optimistically() {
        let mut player = PlayerVideo::<u32>::default();
        player.ensure_generation(320, 240, make).unwrap();
        let (slot, generation, index) = player.take_slot(320, 240).expect("active slot");
        assert_eq!((*slot, generation, index), (10, 1, 0));
    }

    #[test]
    fn slots_rotate() {
        let mut player = PlayerVideo::<u32>::default();
        player.ensure_generation(320, 240, make).unwrap();
        let indexes: Vec<u8> = (0..4)
            .map(|_| player.take_slot(320, 240).expect("slot").2)
            .collect();
        assert_eq!(indexes, vec![0, 1, 2, 0]);
    }

    #[test]
    fn resolution_change_waits_for_ack() {
        let mut player = PlayerVideo::<u32>::default();
        player.ensure_generation(320, 240, make).unwrap();
        assert!(player.take_slot(320, 240).is_some());

        player.ensure_generation(640, 480, make).unwrap();
        // active is stale, replacement pending: no slot to write
        assert!(player.take_slot(640, 480).is_none());

        player.ack(2);
        let (slot, generation, _) = player.take_slot(640, 480).expect("switched");
        assert_eq!((*slot, generation), (20, 2));
    }

    #[test]
    fn foreign_ack_is_ignored() {
        let mut player = PlayerVideo::<u32>::default();
        player.ensure_generation(320, 240, make).unwrap();
        player.ensure_generation(640, 480, make).unwrap();
        player.ack(99);
        assert!(player.take_slot(640, 480).is_none());
    }

    #[test]
    fn create_failure_propagates_and_retries() {
        let mut player = PlayerVideo::<u32>::default();
        assert!(
            player
                .ensure_generation(320, 240, |_| anyhow::bail!("boom"))
                .is_err()
        );
        // the failed attempt burned a generation number but left no state
        player.ensure_generation(320, 240, make).unwrap();
        let (_, generation, _) = player.take_slot(320, 240).expect("slot");
        assert_eq!(generation, 2);
    }
}
