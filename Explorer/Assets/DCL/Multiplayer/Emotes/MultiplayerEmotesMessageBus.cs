using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Optimization.Multithreading;
using DCL.Optimization.Pools;
using DCL.Utilities;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Emotes
{
    public class MultiplayerEmotesMessageBus : IDisposable, IEmotesMessageBus
    {
        private const float LATENCY = 0f;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly MultiplayerDebugSettings settings;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;

        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly EmotesScheduler messageScheduler;
        private uint nextIncrementalId = 1;

        private readonly HashSet<RemoteEmoteIntention> emoteIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly MutexSync sync = new();

        public MultiplayerEmotesMessageBus(IMessagePipesHub messagePipesHub,
            MultiplayerDebugSettings settings,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy)
        {
            this.messagePipesHub = messagePipesHub;
            this.settings = settings;
            this.userBlockingCacheProxy = userBlockingCacheProxy;

            messageScheduler = new EmotesScheduler();

            this.messagePipesHub.IslandPipe().Subscribe<PlayerEmote>(Packet.MessageOneofCase.PlayerEmote, OnMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<PlayerEmote>(Packet.MessageOneofCase.PlayerEmote, OnMessageReceived);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() =>
            new (sync, emoteIntentions);

        public void Send(URN emote, bool loopCyclePassed, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("EmoteMessagesBus is disposed");

            float timestamp = Time.unscaledTime;

            SendTo(emote, timestamp, messagePipesHub.IslandPipe(), socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress);
            SendTo(emote, timestamp, messagePipesHub.ScenePipe(), socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress);

            if (settings.SelfSending)
                SelfSendWithDelayAsync(emote, timestamp, isUsingSocialEmoteOutcome, socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress).Forget();
        }

        public void OnPlayerRemoved(string walletId) =>
            messageScheduler.RemoveWallet(walletId);

        private void SendTo(URN emoteId, float timestamp, IMessagePipe messagePipe, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress)
        {
            MessageWrap<PlayerEmote> emote = messagePipe.NewMessage<PlayerEmote>();

            emote.Payload.IncrementalId = nextIncrementalId++;
            emote.Payload.Urn = emoteId;
            emote.Payload.Timestamp = timestamp;
            emote.Payload.SocialEmoteOutcome = socialEmoteOutcomeIndex;
            emote.Payload.IsReacting = isReactingToSocialEmote;
            emote.Payload.SocialEmoteInitiator = socialEmoteInitiatorWalletAddress?? string.Empty;
            emote.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();

            Debug.LogError("SENT TO [" + emote.Payload.IncrementalId + "]: " + emote.Payload.Urn + " reacting? " + emote.Payload.IsReacting );
        }

        private async UniTaskVoid SelfSendWithDelayAsync(URN urn, float timestamp, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(LATENCY), cancellationToken: cancellationTokenSource.Token);
            Inbox(RemotePlayerMovementComponent.TEST_ID, urn, timestamp, isUsingSocialEmoteOutcome, socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress);
        }

        private void OnMessageReceived(ReceivedMessage<PlayerEmote> receivedMessage)
        {
            using (receivedMessage)
            {
                if (cancellationTokenSource.Token.IsCancellationRequested || IsUserBlocked(receivedMessage.FromWalletId))
                {
                    messageScheduler.RemoveWallet(receivedMessage.FromWalletId);
                    return;
                }

                // Use timestamp from message if present (non-zero), otherwise fallback to current Unity time
                float timestamp = receivedMessage.Payload.Timestamp != 0f
                    ? receivedMessage.Payload.Timestamp
                    : Time.unscaledTime;

                Debug.LogError("RECEIVED [" + receivedMessage.Payload.IncrementalId + "]");

                Inbox(receivedMessage.FromWalletId, receivedMessage.Payload.Urn, timestamp, receivedMessage.Payload.SocialEmoteOutcome > -1, receivedMessage.Payload.SocialEmoteOutcome, receivedMessage.Payload.IsReacting, receivedMessage.Payload.SocialEmoteInitiator);
            }
        }

        private bool IsUserBlocked(string userAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.UserIsBlocked(userAddress);

        private void Inbox(string walletId, URN emoteURN, float timestamp, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress)
        {
            if (messageScheduler.TryPass(walletId, timestamp) == false)
                return;

            using (sync.GetScope())
            {
                Debug.LogError("INBOX: " + walletId + " " + emoteURN + " is outcome? " + isUsingSocialEmoteOutcome + " reacting? " + isReactingToSocialEmote);
                emoteIntentions.Add(new RemoteEmoteIntention(emoteURN, walletId, timestamp, isUsingSocialEmoteOutcome, socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress));
            }
        }

        public void SaveForRetry(RemoteEmoteIntention intention)
        {
            emoteIntentions.Add(intention);
        }
    }
}
