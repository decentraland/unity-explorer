using System;
using System.Collections.Generic;
using DCL.Chat.History;
using DCL.FeatureFlags;
using DCL.Web3.Identities;
using NUnit.Framework;

namespace DCL.Chat.ChatReactions.Tests
{
    [TestFixture]
    public class ChatMessageReactionServiceShould
    {
        private string ownWallet;
        private ChatHistory chatHistory;
        private FakeReactionBus fakeBus;
        private ChatMessageReactionService service;

        [SetUp]
        public void SetUp()
        {
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());

            chatHistory = new ChatHistory();
            fakeBus = new FakeReactionBus();

            var identity = new IWeb3Identity.Random();
            ownWallet = identity.Address;

            var identityCache = new IWeb3IdentityCache.Fake(identity);

            service = new ChatMessageReactionService(fakeBus, chatHistory, identityCache);
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void ResolveMessageAddedViaEvent()
        {
            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatMessage msg = CreateMessage("walletA", 1000);
            channel.AddMessage(msg);

            // ToggleReaction should succeed — message was registered via MessageAdded event
            service.ToggleReaction(msg.MessageId, emojiIndex: 0);

            ReactionSet? reactions = channel.GetReactions(msg.MessageId);
            Assert.That(reactions, Is.Not.Null);
            Assert.That(reactions!.HasReacted(0, ownWallet), Is.True);
        }

        [Test]
        public void ResolveMessageRegisteredViaBatchRegistration()
        {
            var channelId = new ChatChannel.ChannelId("dm_user1");
            ChatChannel channel = chatHistory.AddOrGetChannel(channelId, ChatChannel.ChatChannelType.USER);

            // FillChannel does NOT fire MessageAdded — simulates history load
            var messages = new List<ChatMessage> { CreateMessage("walletA", 2000) };
            channel.FillChannel(messages);

            // Explicit batch registration
            service.RegisterChannelMessages(channel);

            string messageId = messages[0].MessageId;
            service.ToggleReaction(messageId, emojiIndex: 1);

            ReactionSet? reactions = channel.GetReactions(messageId);
            Assert.That(reactions, Is.Not.Null);
            Assert.That(reactions!.HasReacted(1, ownWallet), Is.True);
        }

        [Test]
        public void PurgeOnlyTargetChannelOnClear()
        {
            ChatChannel nearby = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatChannel dm = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("dm_user1"), ChatChannel.ChatChannelType.USER);

            ChatMessage nearbyMsg = CreateMessage("walletA", 1000);
            ChatMessage dmMsg = CreateMessage("walletB", 2000);

            nearby.AddMessage(nearbyMsg);
            dm.AddMessage(dmMsg);

            // Clear nearby — should purge nearby entries only
            chatHistory.ClearChannel(nearby.Id);

            // DM message should still be resolvable
            service.ToggleReaction(dmMsg.MessageId, emojiIndex: 0);
            ReactionSet? dmReactions = dm.GetReactions(dmMsg.MessageId);
            Assert.That(dmReactions, Is.Not.Null);
            Assert.That(dmReactions!.HasReacted(0, ownWallet), Is.True);

            // Nearby message should no longer be resolvable (no reaction added)
            service.ToggleReaction(nearbyMsg.MessageId, emojiIndex: 0);
            ReactionSet? nearbyReactions = nearby.GetReactions(nearbyMsg.MessageId);
            Assert.That(nearbyReactions, Is.Null);
        }

        [Test]
        public void PurgeOnlyTargetChannelOnRemove()
        {
            ChatChannel nearby = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatChannel dm = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("dm_user1"), ChatChannel.ChatChannelType.USER);

            ChatMessage nearbyMsg = CreateMessage("walletA", 1000);
            ChatMessage dmMsg = CreateMessage("walletB", 2000);

            nearby.AddMessage(nearbyMsg);
            dm.AddMessage(dmMsg);

            // Remove DM channel — should purge DM entries only
            chatHistory.RemoveChannel(dm.Id);

            // Nearby message should still be resolvable
            service.ToggleReaction(nearbyMsg.MessageId, emojiIndex: 0);
            ReactionSet? nearbyReactions = nearby.GetReactions(nearbyMsg.MessageId);
            Assert.That(nearbyReactions, Is.Not.Null);
            Assert.That(nearbyReactions!.HasReacted(0, ownWallet), Is.True);
        }

        [Test]
        public void NotThrowWhenTogglingUnknownMessage()
        {
            Assert.DoesNotThrow(() => service.ToggleReaction("nonexistent_id", emojiIndex: 0));
        }

        [Test]
        public void ToggleOffExistingReaction()
        {
            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatMessage msg = CreateMessage("walletA", 1000);
            channel.AddMessage(msg);

            // Add reaction
            service.ToggleReaction(msg.MessageId, emojiIndex: 2);
            Assert.That(channel.GetReactions(msg.MessageId)!.HasReacted(2, ownWallet), Is.True);

            // Toggle again — should remove
            service.ToggleReaction(msg.MessageId, emojiIndex: 2);
            ReactionSet? reactions = channel.GetReactions(msg.MessageId);

            // ReactionSet is removed entirely when empty
            Assert.That(reactions, Is.Null);
        }

        [Test]
        public void NotRegisterNewMessagesAfterDispose()
        {
            service.Dispose();

            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatMessage msg = CreateMessage("walletA", 1000);
            channel.AddMessage(msg);

            // Message was added after dispose — should not be resolvable
            service.ToggleReaction(msg.MessageId, emojiIndex: 0);

            ReactionSet? reactions = channel.GetReactions(msg.MessageId);
            Assert.That(reactions, Is.Null);
        }

        [Test]
        public void HandleMultipleMessagesInSameChannel()
        {
            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatMessage msg1 = CreateMessage("walletA", 1000);
            ChatMessage msg2 = CreateMessage("walletA", 2000);
            ChatMessage msg3 = CreateMessage("walletB", 3000);

            channel.AddMessage(msg1);
            channel.AddMessage(msg2);
            channel.AddMessage(msg3);

            // All three should be resolvable
            service.ToggleReaction(msg1.MessageId, emojiIndex: 0);
            service.ToggleReaction(msg2.MessageId, emojiIndex: 1);
            service.ToggleReaction(msg3.MessageId, emojiIndex: 2);

            Assert.That(channel.GetReactions(msg1.MessageId)!.HasReacted(0, ownWallet), Is.True);
            Assert.That(channel.GetReactions(msg2.MessageId)!.HasReacted(1, ownWallet), Is.True);
            Assert.That(channel.GetReactions(msg3.MessageId)!.HasReacted(2, ownWallet), Is.True);
        }

        [Test]
        public void FireReactionPersistenceRequestedOnToggle()
        {
            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatMessage msg = CreateMessage("walletA", 1000);
            channel.AddMessage(msg);

            ChatChannel.ChannelId? capturedChannelId = null;
            string? capturedMessageId = null;
            int capturedEmoji = -1;
            bool capturedIsRemoval = false;

            service.ReactionPersistenceRequested += (chId, mId, emoji, wallet, isRemoval) =>
            {
                capturedChannelId = chId;
                capturedMessageId = mId;
                capturedEmoji = emoji;
                capturedIsRemoval = isRemoval;
            };

            service.ToggleReaction(msg.MessageId, emojiIndex: 3);

            Assert.That(capturedChannelId, Is.Not.Null);
            Assert.That(capturedChannelId!.Value.Id, Is.EqualTo("nearby"));
            Assert.That(capturedMessageId, Is.EqualTo(msg.MessageId));
            Assert.That(capturedEmoji, Is.EqualTo(3));
            Assert.That(capturedIsRemoval, Is.False);
        }

        [Test]
        public void ReRegisterMessagesAfterClearAndReload()
        {
            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("dm_user1"), ChatChannel.ChatChannelType.USER);

            ChatMessage msg = CreateMessage("walletA", 1000);
            channel.AddMessage(msg);
            string messageId = msg.MessageId;

            // Clear the channel
            chatHistory.ClearChannel(channel.Id);

            // Re-add the same message (simulates re-opening channel and loading history)
            var reloaded = new List<ChatMessage> { CreateMessage("walletA", 1000) };
            channel.FillChannel(reloaded);
            service.RegisterChannelMessages(channel);

            // Should be resolvable again
            service.ToggleReaction(messageId, emojiIndex: 0);

            ReactionSet? reactions = channel.GetReactions(messageId);
            Assert.That(reactions, Is.Not.Null);
            Assert.That(reactions!.HasReacted(0, ownWallet), Is.True);
        }

        // ── Test helpers ─────────────────────────────────────────

        private static ChatMessage CreateMessage(string walletAddress, double timestamp) =>
            new ("hello", "User", walletAddress, false, walletAddress, timestamp);

        /// <summary>
        /// Minimal fake that records calls. Avoids NSubstitute for value-type arg capture.
        /// </summary>
        private sealed class FakeReactionBus : IReactionMessageBus
        {
            public readonly List<(int EmojiIndex, string MessageId, ReactionChannelRouting Routing)> MessageSends = new ();

            public event Action<ReactionReceivedArgs>? ReactionReceived;

            public void SendSituationalReaction(int emojiIndex, int count = 1, float overrideTimestamp = 0f) { }

            public void SendMessageReaction(int emojiIndex, string messageId, ReactionChannelRouting routing) =>
                MessageSends.Add((emojiIndex, messageId, routing));

            public void Dispose() { }
        }
    }
}

