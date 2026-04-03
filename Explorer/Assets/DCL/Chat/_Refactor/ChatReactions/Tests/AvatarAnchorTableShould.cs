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
            // Act
            byte slot = table.Allocate("wallet_A", Vector3.one);

            // Assert
            Assert.That(slot, Is.Not.EqualTo(AvatarAnchorTable.ANCHOR_NONE));
            Assert.That(table.IsActive(slot), Is.True);
        }

        [Test]
        public void ReturnSameSlotForExistingWallet()
        {
            // Arrange
            byte first = table.Allocate("wallet_A", Vector3.zero);

            // Act
            byte second = table.Allocate("wallet_A", Vector3.one);

            // Assert
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void UpdatePositionOnReallocation()
        {
            // Arrange
            var updatedPos = new Vector3(5, 10, 15);
            byte slot = table.Allocate("wallet_A", Vector3.zero);

            // Act
            table.Allocate("wallet_A", updatedPos);

            // Assert
            Assert.That(table.GetPosition(slot), Is.EqualTo(updatedPos));
        }

        [Test]
        public void AllocateDifferentSlotsForDifferentWallets()
        {
            // Act
            byte a = table.Allocate("wallet_A", Vector3.zero);
            byte b = table.Allocate("wallet_B", Vector3.one);

            // Assert
            Assert.That(a, Is.Not.EqualTo(b));
        }

        // Verifies that a slot freed by Refresh is reclaimed by the next allocation.
        [Test]
        public void ReuseDeactivatedSlots()
        {
            // Arrange — allocate 3 wallets to slots 0, 1, 2
            table.Allocate("wallet_A", Vector3.zero);
            table.Allocate("wallet_B", Vector3.zero);
            table.Allocate("wallet_C", Vector3.zero);

            // Deactivate wallet_B (slot 1) via Refresh that returns null for it
            var provider = new SelectiveAvatarPosition("wallet_A", "wallet_C");
            table.Refresh(provider);

            // Act — allocating a new wallet should reuse slot 1 (first inactive)
            byte newSlot = table.Allocate("wallet_D", Vector3.zero);

            // Assert
            Assert.That(newSlot, Is.EqualTo(1));
        }

        // Confirms the internal scan limit contracts when trailing slots become inactive.
        [Test]
        public void ShrinkScanLimitWhenTrailingSlotsDeactivate()
        {
            // Arrange — allocate 3 wallets to slots 0, 1, 2
            table.Allocate("wallet_A", Vector3.zero);
            table.Allocate("wallet_B", Vector3.zero);
            table.Allocate("wallet_C", Vector3.zero);

            // Deactivate wallet_C (slot 2, the trailing slot)
            var provider = new SelectiveAvatarPosition("wallet_A", "wallet_B");
            table.Refresh(provider);

            // Act
            byte slot = table.Allocate("wallet_D", Vector3.zero);

            // Assert — should go to slot 2 since scanLimit shrunk
            Assert.That(slot, Is.EqualTo(2));
        }

        // Ensures the scan limit stays wide when a gap opens in the middle, not the tail.
        [Test]
        public void NotShrinkScanLimitWhenMiddleSlotDeactivates()
        {
            // Arrange — allocate 3 wallets to slots 0, 1, 2
            table.Allocate("wallet_A", Vector3.zero);
            table.Allocate("wallet_B", Vector3.zero);
            table.Allocate("wallet_C", Vector3.zero);

            // Deactivate wallet_A (slot 0, NOT trailing) — scanLimit stays 3
            var provider = new SelectiveAvatarPosition("wallet_B", "wallet_C");
            table.Refresh(provider);

            // Act
            byte slot = table.Allocate("wallet_D", Vector3.zero);

            // Assert — wallet_C at slot 2 is still active; new wallet reuses slot 0
            Assert.That(table.IsActive(2), Is.True);
            Assert.That(slot, Is.EqualTo(0));
        }

        [Test]
        public void ShrinkScanLimitToZeroWhenAllDeactivate()
        {
            // Arrange
            table.Allocate("wallet_A", Vector3.zero);
            table.Allocate("wallet_B", Vector3.zero);

            // Act — deactivate all anchors
            var emptyProvider = new SelectiveAvatarPosition();
            table.Refresh(emptyProvider);

            // Assert — both slots inactive, next allocation reuses slot 0
            Assert.That(table.IsActive(0), Is.False);
            Assert.That(table.IsActive(1), Is.False);

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
            // Arrange
            table.Allocate("wallet_A", Vector3.zero);
            var updatedPos = new Vector3(10, 20, 30);
            var provider = new FixedPositionAvatarPosition(updatedPos);

            // Act
            table.Refresh(provider);

            // Assert
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
