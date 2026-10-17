using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace DCL.Chat.History.Tests
{
    [TestFixture]
    public class ReactionSetShould
    {
        private ReactionSet reactionSet = null!;
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
            Assert.AreEqual(2, reactors!.Count);
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

        [Test]
        public void RejectNewDistinctEmojiBeyondHardCap()
        {
            // Arrange
            for (int emoji = 0; emoji < ReactionSet.MAX_DISTINCT_EMOJIS; emoji++)
                Assert.IsTrue(reactionSet.AddReaction(emoji, "wallet_a"));

            // Act
            bool result = reactionSet.AddReaction(ReactionSet.MAX_DISTINCT_EMOJIS, "wallet_a");

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(ReactionSet.MAX_DISTINCT_EMOJIS, reactionSet.DistinctEmojiCount);
        }

        [Test]
        public void AcceptNewWalletOnExistingEmojiAtHardCap()
        {
            // Arrange
            for (int emoji = 0; emoji < ReactionSet.MAX_DISTINCT_EMOJIS; emoji++)
                reactionSet.AddReaction(emoji, "wallet_a");

            // Act
            bool result = reactionSet.AddReaction(0, "wallet_b");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(2, reactionSet.GetReactors(0)!.Count);
        }

        [Test]
        public void AcceptNewDistinctEmojiAfterRemovalFreesSlotAtHardCap()
        {
            // Arrange
            for (int emoji = 0; emoji < ReactionSet.MAX_DISTINCT_EMOJIS; emoji++)
                reactionSet.AddReaction(emoji, "wallet_a");

            reactionSet.RemoveReaction(0, "wallet_a");

            // Act
            bool result = reactionSet.AddReaction(ReactionSet.MAX_DISTINCT_EMOJIS, "wallet_a");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(ReactionSet.MAX_DISTINCT_EMOJIS, reactionSet.DistinctEmojiCount);
        }

        [Test]
        public void RejectNewReactorBeyondPerEmojiHardCap()
        {
            // Arrange
            for (int i = 0; i < ReactionSet.MAX_REACTORS_PER_EMOJI; i++)
                Assert.IsTrue(reactionSet.AddReaction(1, $"wallet_{i}"));

            // Act
            bool result = reactionSet.AddReaction(1, $"wallet_{ReactionSet.MAX_REACTORS_PER_EMOJI}");

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(ReactionSet.MAX_REACTORS_PER_EMOJI, reactionSet.GetReactors(1)!.Count);
        }

        [Test]
        public void AcceptExistingReactorAtCapAndPruneEmojiAfterFullRemoval()
        {
            // Arrange
            for (int i = 0; i < ReactionSet.MAX_REACTORS_PER_EMOJI; i++)
                reactionSet.AddReaction(1, $"wallet_{i}");

            // Act
            bool reAddResult = reactionSet.AddReaction(1, "wallet_0");
            bool newWalletResult = reactionSet.AddReaction(1, "wallet_new");

            // Assert: an already-present wallet is an unaffected no-op; a genuinely new wallet is still rejected
            Assert.IsFalse(reAddResult);
            Assert.IsFalse(newWalletResult);
            Assert.AreEqual(ReactionSet.MAX_REACTORS_PER_EMOJI, reactionSet.GetReactors(1)!.Count);

            // Act
            for (int i = 0; i < ReactionSet.MAX_REACTORS_PER_EMOJI; i++)
                Assert.IsTrue(reactionSet.RemoveReaction(1, $"wallet_{i}"));

            // Assert: the emoji is fully pruned once its reactor set empties
            Assert.IsTrue(reactionSet.IsEmpty);
            Assert.IsNull(reactionSet.GetReactors(1));
        }
    }
}
