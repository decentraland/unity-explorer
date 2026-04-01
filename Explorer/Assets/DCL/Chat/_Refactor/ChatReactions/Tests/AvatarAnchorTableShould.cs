using System.Collections.Generic;
using DCL.Chat.ChatReactions.Simulation.World;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class AvatarAnchorTableShould
    {
        private AvatarAnchorTable table;

        [SetUp]
        public void SetUp()
        {
            table = new AvatarAnchorTable();
        }

        [Test]
        public void AllocateSlotForNewWallet()
        {
            byte slot = table.Allocate("wallet_A", Vector3.one);

            Assert.That(slot, Is.Not.EqualTo(AvatarAnchorTable.ANCHOR_NONE));
            Assert.That(table.IsActive(slot), Is.True);
        }

        [Test]
        public void ReturnSameSlotForExistingWallet()
        {
            byte first = table.Allocate("wallet_A", Vector3.zero);
            byte second = table.Allocate("wallet_A", Vector3.one);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void UpdatePositionOnReallocation()
        {
            var updatedPos = new Vector3(5, 10, 15);
            byte slot = table.Allocate("wallet_A", Vector3.zero);
            table.Allocate("wallet_A", updatedPos);

            Assert.That(table.GetPosition(slot), Is.EqualTo(updatedPos));
        }

        [Test]
        public void AllocateDifferentSlotsForDifferentWallets()
        {
            byte a = table.Allocate("wallet_A", Vector3.zero);
            byte b = table.Allocate("wallet_B", Vector3.one);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void ReuseDeactivatedSlots()
        {
            // Allocate 3 wallets → slots 0, 1, 2
            table.Allocate("wallet_A", Vector3.zero);
            table.Allocate("wallet_B", Vector3.zero);
            table.Allocate("wallet_C", Vector3.zero);

            // Deactivate wallet_B (slot 1) by having Refresh remove it
            // We simulate this indirectly: allocate enough to fill, then rely on
            // FindFirstInactiveSlot. Instead, test that after a Refresh that removes
            // an avatar, the slot becomes reusable.

            // Use Refresh with a provider that returns null for wallet_B
            var provider = new SelectiveAvatarPosition("wallet_A", "wallet_C");
            table.Refresh(provider);

            // wallet_B's slot should now be inactive
            // Allocating a new wallet should reuse slot 1 (first inactive)
            byte newSlot = table.Allocate("wallet_D", Vector3.zero);
            Assert.That(newSlot, Is.EqualTo(1));
        }

        [Test]
        public void ShrinkScanLimitWhenTrailingSlotsDeactivate()
        {
            // Allocate 3 wallets → slots 0, 1, 2 → scanLimit = 3
            table.Allocate("wallet_A", Vector3.zero);
            table.Allocate("wallet_B", Vector3.zero);
            table.Allocate("wallet_C", Vector3.zero);

            // Deactivate wallet_C (slot 2, the trailing slot)
            var provider = new SelectiveAvatarPosition("wallet_A", "wallet_B");
            table.Refresh(provider);

            // Now allocate wallet_D — should go to slot 2 (scanLimit shrunk, slot 2 is free)
            byte slot = table.Allocate("wallet_D", Vector3.zero);
            Assert.That(slot, Is.EqualTo(2));
        }

        [Test]
        public void NotShrinkScanLimitWhenMiddleSlotDeactivates()
        {
            // Allocate 3 wallets → slots 0, 1, 2 → scanLimit = 3
            table.Allocate("wallet_A", Vector3.zero);
            table.Allocate("wallet_B", Vector3.zero);
            table.Allocate("wallet_C", Vector3.zero);

            // Deactivate wallet_A (slot 0, NOT trailing) — scanLimit stays 3
            var provider = new SelectiveAvatarPosition("wallet_B", "wallet_C");
            table.Refresh(provider);

            // wallet_C at slot 2 should still be active
            Assert.That(table.IsActive(2), Is.True);

            // New allocation should reuse slot 0
            byte slot = table.Allocate("wallet_D", Vector3.zero);
            Assert.That(slot, Is.EqualTo(0));
        }

        [Test]
        public void ShrinkScanLimitToZeroWhenAllDeactivate()
        {
            table.Allocate("wallet_A", Vector3.zero);
            table.Allocate("wallet_B", Vector3.zero);

            // Deactivate all
            var emptyProvider = new SelectiveAvatarPosition();
            table.Refresh(emptyProvider);

            Assert.That(table.IsActive(0), Is.False);
            Assert.That(table.IsActive(1), Is.False);

            // Next allocation should start from slot 0 again
            byte slot = table.Allocate("wallet_C", Vector3.one);
            Assert.That(slot, Is.EqualTo(0));
        }

        [Test]
        public void TrackPositionCorrectly()
        {
            var pos = new Vector3(1, 2, 3);
            byte slot = table.Allocate("wallet_A", pos);

            Assert.That(table.GetPosition(slot), Is.EqualTo(pos));
        }

        [Test]
        public void RefreshPositionsFromProvider()
        {
            table.Allocate("wallet_A", Vector3.zero);

            var updatedPos = new Vector3(10, 20, 30);
            var provider = new FixedPositionAvatarPosition(updatedPos);
            table.Refresh(provider);

            Assert.That(table.GetPosition(0), Is.EqualTo(updatedPos));
        }

        [Test]
        public void ReportInactiveForAnchorNone()
        {
            Assert.That(table.IsActive(AvatarAnchorTable.ANCHOR_NONE), Is.False);
        }

        // ── Test helpers ─────────────────────────────────────────

        /// <summary>
        /// Returns positions only for wallets in the allowed set; null for all others.
        /// </summary>
        private class SelectiveAvatarPosition : IAvatarReactionPosition
        {
            private readonly HashSet<string> allowedWallets;

            public SelectiveAvatarPosition(params string[] wallets)
            {
                allowedWallets = new HashSet<string>(wallets);
            }

            public Vector3? GetHeadPosition(string walletId) =>
                allowedWallets.Contains(walletId) ? Vector3.zero : null;

            public Vector3? GetLocalPlayerHeadPosition() => Vector3.zero;
            public List<Vector3> GetAllNearbyHeadPositions() => new ();
            public int LastNearbyCount => 0;
            public int GetNearbyAvatarCount() => 0;
        }

        private class FixedPositionAvatarPosition : IAvatarReactionPosition
        {
            private readonly Vector3 position;

            public FixedPositionAvatarPosition(Vector3 position)
            {
                this.position = position;
            }

            public Vector3? GetHeadPosition(string walletId) => position;
            public Vector3? GetLocalPlayerHeadPosition() => position;
            public List<Vector3> GetAllNearbyHeadPositions() => new ();
            public int LastNearbyCount => 0;
            public int GetNearbyAvatarCount() => 0;
        }
    }
}
