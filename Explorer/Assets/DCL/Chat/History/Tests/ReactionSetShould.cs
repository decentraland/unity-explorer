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
            bool result = reactionSet.AddReaction(1, "wallet_a");
            Assert.IsTrue(result);
            Assert.IsFalse(reactionSet.IsEmpty);
        }

        [Test]
        public void ReturnFalseWhenAddingDuplicateReaction()
        {
            reactionSet.AddReaction(1, "wallet_a");
            bool result = reactionSet.AddReaction(1, "wallet_a");
            Assert.IsFalse(result);
        }

        [Test]
        public void AddDifferentEmojisSameWallet()
        {
            Assert.IsTrue(reactionSet.AddReaction(1, "wallet_a"));
            Assert.IsTrue(reactionSet.AddReaction(2, "wallet_a"));

            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(2, countsBuffer.Count);
            Assert.AreEqual((1, 1), countsBuffer[0]);
            Assert.AreEqual((2, 1), countsBuffer[1]);
        }

        [Test]
        public void AddSameEmojiDifferentWallets()
        {
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(1, "wallet_b");

            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(1, countsBuffer.Count);
            Assert.AreEqual((1, 2), countsBuffer[0]);
        }

        [Test]
        public void ReturnTrueWhenRemovingExistingReaction()
        {
            reactionSet.AddReaction(1, "wallet_a");
            bool result = reactionSet.RemoveReaction(1, "wallet_a");
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
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(1, "wallet_b");
            reactionSet.RemoveReaction(1, "wallet_a");

            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(1, countsBuffer.Count);
            Assert.AreEqual((1, 1), countsBuffer[0]);
        }

        [Test]
        public void CleanUpEmptyEmojiAfterRemoval()
        {
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.RemoveReaction(1, "wallet_a");

            Assert.IsTrue(reactionSet.IsEmpty);
            Assert.IsNull(reactionSet.GetReactors(1));
        }

        [Test]
        public void ReturnCorrectHasReactedState()
        {
            reactionSet.AddReaction(1, "wallet_a");

            Assert.IsTrue(reactionSet.HasReacted(1, "wallet_a"));
            Assert.IsFalse(reactionSet.HasReacted(1, "wallet_b"));
            Assert.IsFalse(reactionSet.HasReacted(2, "wallet_a"));
        }

        [Test]
        public void PreserveInsertionOrderInAggregateCounts()
        {
            reactionSet.AddReaction(3, "wallet_a");
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(2, "wallet_a");

            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(3, countsBuffer.Count);
            Assert.AreEqual(3, countsBuffer[0].EmojiIndex);
            Assert.AreEqual(1, countsBuffer[1].EmojiIndex);
            Assert.AreEqual(2, countsBuffer[2].EmojiIndex);
        }

        [Test]
        public void SurvivePartialRemovalInInsertionOrder()
        {
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(1, "wallet_b");
            reactionSet.AddReaction(2, "wallet_a");
            reactionSet.RemoveReaction(1, "wallet_a");

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
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(1, "wallet_b");

            var reactors = reactionSet.GetReactors(1);
            Assert.IsNotNull(reactors);
            Assert.AreEqual(2, reactors.Count);
            Assert.IsTrue(reactors.Contains("wallet_a"));
            Assert.IsTrue(reactors.Contains("wallet_b"));
        }

        [Test]
        public void RemoveEverythingOnClear()
        {
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(2, "wallet_b");
            reactionSet.Clear();

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

        [Test]
        public void ReappearRemovedEmojiAtEndOfInsertionOrder()
        {
            reactionSet.AddReaction(1, "wallet_a");
            reactionSet.AddReaction(2, "wallet_a");
            reactionSet.RemoveReaction(1, "wallet_a");
            reactionSet.AddReaction(1, "wallet_b");

            reactionSet.GetAggregateCounts(countsBuffer);
            Assert.AreEqual(2, countsBuffer.Count);
            Assert.AreEqual(2, countsBuffer[0].EmojiIndex);
            Assert.AreEqual(1, countsBuffer[1].EmojiIndex);
        }
    }
}
