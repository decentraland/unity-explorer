using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.MessageBus.Deduplication;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Deduplication;
using DCL.Utilities;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using UnityEngine;
using Utility;

namespace DCL.Chat.ChatReactions
{
    public sealed class MultiplayerReactionMessageBus : IReactionMessageBus
    {
        private const float SELF_SEND_DELAY = 0.05f;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly IWeb3IdentityCache identityCache;
        private readonly CancellationTokenSource cts = new ();
        private readonly bool selfSendEnabled;
        private readonly IMessageDeduplication<float> situationalDedup = new MessageDeduplication<float>();
        private readonly IMessageDeduplication<string> chatReactionDedup = new MessageDeduplication<string>();

        public event Action<ReactionReceivedArgs>? ReactionReceived;

        public MultiplayerReactionMessageBus(
            IMessagePipesHub messagePipesHub,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IWeb3IdentityCache identityCache,
            bool selfSendEnabled = false)
        {
            this.messagePipesHub = messagePipesHub;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.identityCache = identityCache;
            this.selfSendEnabled = selfSendEnabled;

            messagePipesHub.IslandPipe().Subscribe<Reaction>(Packet.MessageOneofCase.Reaction, OnSituationalReactionReceived);
            messagePipesHub.ScenePipe().Subscribe<Reaction>(Packet.MessageOneofCase.Reaction, OnSituationalReactionReceived);

            messagePipesHub.IslandPipe().Subscribe<ChatReaction>(Packet.MessageOneofCase.ChatReaction, OnChatReactionReceived);
            messagePipesHub.ScenePipe().Subscribe<ChatReaction>(Packet.MessageOneofCase.ChatReaction, OnChatReactionReceived);
            messagePipesHub.ChatPipe().Subscribe<ChatReaction>(Packet.MessageOneofCase.ChatReaction, OnChatReactionReceived);
        }

        public void SendSituationalReaction(int emojiIndex)
        {
            if (cts.IsCancellationRequested) return;

            float timestamp = Time.unscaledTime;

            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Sending situational reaction: emoji={emojiIndex} ts={timestamp}");

            SendReactionTo(emojiIndex, timestamp, messagePipesHub.IslandPipe());
            SendReactionTo(emojiIndex, timestamp, messagePipesHub.ScenePipe());

            if (selfSendEnabled)
                SelfSendSituationalAsync(emojiIndex, timestamp).Forget();
        }

        public void SendMessageReaction(int emojiIndex, string messageId)
        {
            if (cts.IsCancellationRequested) return;

            string address = identityCache.Identity?.Address ?? string.Empty;

            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Sending chat reaction: emoji={emojiIndex} messageId={messageId}");

            SendChatReactionTo(emojiIndex, messageId, address, messagePipesHub.IslandPipe());
            SendChatReactionTo(emojiIndex, messageId, address, messagePipesHub.ScenePipe());

            if (selfSendEnabled)
                SelfSendChatReactionAsync(emojiIndex, messageId, address).Forget();
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

        private void SendReactionTo(int emojiIndex, float timestamp, IMessagePipe messagePipe)
        {
            MessageWrap<Reaction> reaction = messagePipe.NewMessage<Reaction>();
            reaction.Payload.EmojiIndex = emojiIndex;
            reaction.Payload.Timestamp = timestamp;
            reaction.SendAndDisposeAsync(cts.Token, DataPacketKind.KindReliable).Forget();
        }

        private void SendChatReactionTo(int emojiIndex, string messageId, string address, IMessagePipe messagePipe)
        {
            MessageWrap<ChatReaction> reaction = messagePipe.NewMessage<ChatReaction>();
            reaction.Payload.EmojiIndex = emojiIndex;
            reaction.Payload.MessageId = messageId;
            reaction.Payload.Address = address;
            reaction.SendAndDisposeAsync(cts.Token, DataPacketKind.KindReliable).Forget();
        }

        private async UniTaskVoid SelfSendSituationalAsync(int emojiIndex, float timestamp)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(SELF_SEND_DELAY), cancellationToken: cts.Token);

                string walletId = identityCache.Identity?.Address ?? string.Empty;

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Self-send situational reaction: emoji={emojiIndex} wallet={walletId}");

                ReactionReceived?.Invoke(new ReactionReceivedArgs(
                    walletId, emojiIndex, 1, ReactionType.Situational, string.Empty));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES); }
        }

        private async UniTaskVoid SelfSendChatReactionAsync(int emojiIndex, string messageId, string address)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(SELF_SEND_DELAY), cancellationToken: cts.Token);

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Self-send chat reaction: emoji={emojiIndex} messageId={messageId}");

                ReactionReceived?.Invoke(new ReactionReceivedArgs(
                    address, emojiIndex, 1, ReactionType.Message, messageId));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES); }
        }

        private void OnSituationalReactionReceived(ReceivedMessage<Reaction> receivedMessage)
        {
            using (receivedMessage)
            {
                if (cts.IsCancellationRequested || IsUserBlocked(receivedMessage.FromWalletId))
                    return;

                float timestamp = receivedMessage.Payload.Timestamp != 0f
                    ? receivedMessage.Payload.Timestamp
                    : Time.unscaledTime;

                if (!situationalDedup.TryPass(receivedMessage.FromWalletId, timestamp))
                    return;

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Received situational reaction: emoji={receivedMessage.Payload.EmojiIndex} from={receivedMessage.FromWalletId}");

                ReactionReceived?.Invoke(new ReactionReceivedArgs(
                    receivedMessage.FromWalletId,
                    receivedMessage.Payload.EmojiIndex,
                    1,
                    ReactionType.Situational,
                    string.Empty));
            }
        }

        private void OnChatReactionReceived(ReceivedMessage<ChatReaction> receivedMessage)
        {
            using (receivedMessage)
            {
                string walletId = !string.IsNullOrEmpty(receivedMessage.Payload.Address)
                    ? receivedMessage.Payload.Address
                    : receivedMessage.FromWalletId;

                if (cts.IsCancellationRequested || IsUserBlocked(walletId))
                    return;

                if (!chatReactionDedup.TryPass(walletId, receivedMessage.Payload.MessageId))
                    return;

                ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[MultiplayerReactionBus] Received chat reaction: emoji={receivedMessage.Payload.EmojiIndex} messageId={receivedMessage.Payload.MessageId} from={walletId}");

                ReactionReceived?.Invoke(new ReactionReceivedArgs(
                    walletId,
                    receivedMessage.Payload.EmojiIndex,
                    1,
                    ReactionType.Message,
                    receivedMessage.Payload.MessageId));
            }
        }

        private bool IsUserBlocked(string userAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.UserIsBlocked(userAddress);
    }
}
