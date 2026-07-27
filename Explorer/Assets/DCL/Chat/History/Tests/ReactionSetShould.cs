using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace DCL.Chat.History.Tests
{
    [TestFixture]
    public class ReactionSetShould
    {
        private ReactionSet reactionSet;
        private readonly List<(int EmojiIndex, int Count)> countsBuffer = new ();

        [SetUp]
        public void SetUp()
        {
            reactionSet = new ReactionSet();
        }

        [Test]
        public void BeEmptyWhenNew()
        {
            Assert.IsTrue(reactionSet.IsEmpty);
        }

        [Test]
        public void ReturnTrueWhenAddingNewReaction()
        {
            // Act
            bool result = reactionSet.AddReaction(1, "wallet_a");

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(reactionSet.IsEmpty);
        }

        [Test]
        public void ReturnFalseWhenAddingDuplicateReaction()
        {
            // Arrange
            reactionSet.AddReaction(1, "wallet_a");

            // Act
            bool result = reactionSet.AddReaction(1, "wallet_a");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void AddDifferentEmojisSameWallet()
        {
            // Act
            Assert.IsTrue(reactionSet.AddReaction(1, "wallet_a"));
            Assert.IsTrue(reactionSet.AddReaction(2, "wallet_a"));

            // Assert
            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(2, countsBuffer.Count);
            Assert.AreEqual((1, 1), countsBuffer[0]);
            Assert.AreEqual((2, 1), countsBuffer[1]);
        }

        [Test]
        public void AddSameEmojiDifferentWallets()
        {
            // Act
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(1, "wallet_b");

            // Assert
            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(1, countsBuffer.Count);
            Assert.AreEqual((1, 2), countsBuffer[0]);
        }

        [Test]
        public void ReturnTrueWhenRemovingExistingReaction()
        {
            // Arrange
            reactionSet.AddReaction(1, "wallet_a");

            // Act
            bool result = reactionSet.RemoveReaction(1, "wallet_a");

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(reactionSet.IsEmpty);
        }

        [Test]
        public void ReturnFalseWhenRemovingNonExistentReaction()
        {
            bool result = reactionSet.RemoveReaction(1, "wallet_a");
            Assert.IsFalse(result);
        }

        [Test]
        public void KeepOtherWalletsWhenRemovingReaction()
        {
            // Arrange
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(1, "wallet_b");

            // Act
            reactionSet.RemoveReaction(1, "wallet_a");

            // Assert
            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(1, countsBuffer.Count);
            Assert.AreEqual((1, 1), countsBuffer[0]);
        }

        // Verifies that the internal emoji entry is fully pruned, not just left with an empty reactor set.
        [Test]
        public void CleanUpEmptyEmojiAfterRemoval()
        {
            // Arrange
            reactionSet.AddReaction(1, "wallet_a");

            // Act
            reactionSet.RemoveReaction(1, "wallet_a");

            // Assert
            Assert.IsTrue(reactionSet.IsEmpty);
            Assert.IsNull(reactionSet.GetReactors(1));
        }

        [Test]
        public void ReturnCorrectHasReactedState()
        {
            // Arrange
            reactionSet.AddReaction(1, "wallet_a");

            // Assert
            Assert.IsTrue(reactionSet.HasReacted(1, "wallet_a"));
            Assert.IsFalse(reactionSet.HasReacted(1, "wallet_b"));
            Assert.IsFalse(reactionSet.HasReacted(2, "wallet_a"));
        }

        [Test]
        public void PreserveInsertionOrderInAggregateCounts()
        {
            // Arrange
            reactionSet.AddReaction(3, "wallet_a");
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(2, "wallet_a");

            // Act
            reactionSet.GetAggregateCounts(countsBuffer);

            // Assert
            Assert.AreEqual(3, countsBuffer.Count);
            Assert.AreEqual(3, countsBuffer[0].EmojiIndex);
            Assert.AreEqual(1, countsBuffer[1].EmojiIndex);
            Assert.AreEqual(2, countsBuffer[2].EmojiIndex);
        }

        // Removing one reactor from a multi-reactor emoji should not change the emoji's position.
        [Test]
        public void SurvivePartialRemovalInInsertionOrder()
        {
            // Arrange
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(1, "wallet_b");
            reactionSet.AddReaction(2, "wallet_a");

            // Act
            reactionSet.RemoveReaction(1, "wallet_a");

            // Assert
            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(2, countsBuffer.Count);
            Assert.AreEqual(1, countsBuffer[0].EmojiIndex);
            Assert.AreEqual(1, countsBuffer[0].Count);
            Assert.AreEqual(2, countsBuffer[1].EmojiIndex);
        }

        [Test]
        public void ReturnNullReactorsWhenEmojiNotPresent()
        {
            Assert.IsNull(reactionSet.GetReactors(99));
        }

        [Test]
        public void ReturnWalletsFromGetReactors()
        {
            // Arrange
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(1, "wallet_b");

            // Act
            var reactors = reactionSet.GetReactors(1);

            // Assert
            Assert.IsNotNull(reactors);
            Assert.AreEqual(2, reactors.Count);
            Assert.IsTrue(reactors.Contains("wallet_a"));
            Assert.IsTrue(reactors.Contains("wallet_b"));
        }

        [Test]
        public void RemoveEverythingOnClear()
        {
            // Arrange
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(2, "wallet_b");

            // Act
            reactionSet.Clear();

            // Assert
            Assert.IsTrue(reactionSet.IsEmpty);
            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(0, countsBuffer.Count);
        }

        [Test]
        public void ToggleAddRemoveAddCorrectly()
        {
            reactionSet.AddReaction(1, "wallet_a");
            Assert.IsTrue(reactionSet.HasReacted(1, "wallet_a"));

            reactionSet.RemoveReaction(1, "wallet_a");
            Assert.IsFalse(reactionSet.HasReacted(1, "wallet_a"));

            reactionSet.AddReaction(1, "wallet_a");
            Assert.IsTrue(reactionSet.HasReacted(1, "wallet_a"));
        }

        // Verifies that re-adding a fully removed emoji places it at the end, not its original position.
        [Test]
        public void ReappearRemovedEmojiAtEndOfInsertionOrder()
        {
            // Arrange
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(2, "wallet_a");
            reactionSet.RemoveReaction(1, "wallet_a");

            // Act
            reactionSet.AddReaction(1, "wallet_b");

            // Assert
            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(2, countsBuffer.Count);
            Assert.AreEqual(2, countsBuffer[0].EmojiIndex);
            Assert.AreEqual(1, countsBuffer[1].EmojiIndex);
        }
    }
}
