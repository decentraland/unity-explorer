using System;
using System.Collections.Generic;
using DCL.Chat.ChatReactions.Networking;
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
            service?.Dispose();
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void ResolveMessageAddedViaEvent()
        {
            // Arrange
            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatMessage msg = CreateMessage("walletA", 1000);
            channel.AddMessage(msg);

            // Act — ToggleReaction should succeed because message was registered via MessageAdded event
            service.ToggleReaction(msg.MessageId, emojiIndex: 0);

            // Assert
            ReactionSet? reactions = channel.GetReactions(msg.MessageId);
            Assert.That(reactions, Is.Not.Null);
            Assert.That(reactions!.HasReacted(0, ownWallet), Is.True);
        }

        // FillChannel bypasses the MessageAdded event, so batch registration must be called explicitly.
        [Test]
        public void ResolveMessageRegisteredViaBatchRegistration()
        {
            // Arrange
            var channelId = new ChatChannel.ChannelId("dm_user1");
            ChatChannel channel = chatHistory.AddOrGetChannel(channelId, ChatChannel.ChatChannelType.USER);

            var messages = new List<ChatMessage> { CreateMessage("walletA", 2000) };
            channel.FillChannel(messages);
            service.RegisterChannelMessages(channel);

            // Act
            string messageId = messages[0].MessageId;
            service.ToggleReaction(messageId, emojiIndex: 1);

            // Assert
            ReactionSet? reactions = channel.GetReactions(messageId);
            Assert.That(reactions, Is.Not.Null);
            Assert.That(reactions!.HasReacted(1, ownWallet), Is.True);
        }

        [Test]
        public void PurgeOnlyTargetChannelOnClear()
        {
            // Arrange
            ChatChannel nearby = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatChannel dm = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("dm_user1"), ChatChannel.ChatChannelType.USER);

            ChatMessage nearbyMsg = CreateMessage("walletA", 1000);
            ChatMessage dmMsg = CreateMessage("walletB", 2000);

            nearby.AddMessage(nearbyMsg);
            dm.AddMessage(dmMsg);

            // Act
            chatHistory.ClearChannel(nearby.Id);

            // Assert — DM message should still be resolvable
            service.ToggleReaction(dmMsg.MessageId, emojiIndex: 0);
            ReactionSet? dmReactions = dm.GetReactions(dmMsg.MessageId);
            Assert.That(dmReactions, Is.Not.Null);
            Assert.That(dmReactions!.HasReacted(0, ownWallet), Is.True);

            // Assert — nearby message should no longer be resolvable
            service.ToggleReaction(nearbyMsg.MessageId, emojiIndex: 0);
            ReactionSet? nearbyReactions = nearby.GetReactions(nearbyMsg.MessageId);
            Assert.That(nearbyReactions, Is.Null);
        }

        [Test]
        public void PurgeOnlyTargetChannelOnRemove()
        {
            // Arrange
            ChatChannel nearby = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatChannel dm = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("dm_user1"), ChatChannel.ChatChannelType.USER);

            ChatMessage nearbyMsg = CreateMessage("walletA", 1000);
            ChatMessage dmMsg = CreateMessage("walletB", 2000);

            nearby.AddMessage(nearbyMsg);
            dm.AddMessage(dmMsg);

            // Act
            chatHistory.RemoveChannel(dm.Id);

            // Assert — nearby message should still be resolvable
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
            // Arrange
            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatMessage msg = CreateMessage("walletA", 1000);
            channel.AddMessage(msg);

            service.ToggleReaction(msg.MessageId, emojiIndex: 2);
            Assert.That(channel.GetReactions(msg.MessageId)!.HasReacted(2, ownWallet), Is.True);

            // Act — toggle the same reaction again to remove it
            service.ToggleReaction(msg.MessageId, emojiIndex: 2);

            // Assert — ReactionSet is removed entirely when empty
            ReactionSet? reactions = channel.GetReactions(msg.MessageId);
            Assert.That(reactions, Is.Null);
        }

        // Verifies that the service stops tracking new messages once disposed.
        [Test]
        public void NotRegisterNewMessagesAfterDispose()
        {
            // Arrange
            service.Dispose();

            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatMessage msg = CreateMessage("walletA", 1000);
            channel.AddMessage(msg);

            // Act
            service.ToggleReaction(msg.MessageId, emojiIndex: 0);

            // Assert
            ReactionSet? reactions = channel.GetReactions(msg.MessageId);
            Assert.That(reactions, Is.Null);
        }

        [Test]
        public void HandleMultipleMessagesInSameChannel()
        {
            // Arrange
            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("nearby"), ChatChannel.ChatChannelType.NEARBY);

            ChatMessage msg1 = CreateMessage("walletA", 1000);
            ChatMessage msg2 = CreateMessage("walletA", 2000);
            ChatMessage msg3 = CreateMessage("walletB", 3000);

            channel.AddMessage(msg1);
            channel.AddMessage(msg2);
            channel.AddMessage(msg3);

            // Act
            service.ToggleReaction(msg1.MessageId, emojiIndex: 0);
            service.ToggleReaction(msg2.MessageId, emojiIndex: 1);
            service.ToggleReaction(msg3.MessageId, emojiIndex: 2);

            // Assert
            Assert.That(channel.GetReactions(msg1.MessageId)!.HasReacted(0, ownWallet), Is.True);
            Assert.That(channel.GetReactions(msg2.MessageId)!.HasReacted(1, ownWallet), Is.True);
            Assert.That(channel.GetReactions(msg3.MessageId)!.HasReacted(2, ownWallet), Is.True);
        }

        [Test]
        public void FireReactionPersistenceRequestedOnToggle()
        {
            // Arrange
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

            // Act
            service.ToggleReaction(msg.MessageId, emojiIndex: 3);

            // Assert
            Assert.That(capturedChannelId, Is.Not.Null);
            Assert.That(capturedChannelId!.Value.Id, Is.EqualTo("nearby"));
            Assert.That(capturedMessageId, Is.EqualTo(msg.MessageId));
            Assert.That(capturedEmoji, Is.EqualTo(3));
            Assert.That(capturedIsRemoval, Is.False);
        }

        // Simulates re-opening a channel after it was cleared and its history reloaded.
        [Test]
        public void ReRegisterMessagesAfterClearAndReload()
        {
            // Arrange
            ChatChannel channel = chatHistory.AddOrGetChannel(
                new ChatChannel.ChannelId("dm_user1"), ChatChannel.ChatChannelType.USER);

            ChatMessage msg = CreateMessage("walletA", 1000);
            channel.AddMessage(msg);
            string messageId = msg.MessageId;

            chatHistory.ClearChannel(channel.Id);

            var reloaded = new List<ChatMessage> { CreateMessage("walletA", 1000) };
            channel.FillChannel(reloaded);
            service.RegisterChannelMessages(channel);

            // Act
            service.ToggleReaction(messageId, emojiIndex: 0);

            // Assert
            ReactionSet? reactions = channel.GetReactions(messageId);
            Assert.That(reactions, Is.Not.Null);
            Assert.That(reactions!.HasReacted(0, ownWallet), Is.True);
        }

        // ── Test helpers ─────────────────────────────────────────

        private static ChatMessage CreateMessage(string walletAddress, double timestamp) =>
            new ("hello", "User", walletAddress, false, walletAddress, timestamp);

    }
}

