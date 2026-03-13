using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
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
using Utility;

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
        private readonly HashSet<RemoteEmoteStopIntention> emoteStopIntentions = new (PoolConstants.AVATARS_COUNT);

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

        public OwnedBunch<RemoteEmoteStopIntention> EmoteStopIntentions() =>
            new (sync, emoteStopIntentions);

        public void Send(URN emote, bool loopCyclePassed, AvatarEmoteMask mask)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("EmoteMessagesBus is disposed");

            float timestamp = Time.unscaledTime;

            SendTo(emote, timestamp, mask, messagePipesHub.IslandPipe());
            SendTo(emote, timestamp, mask, messagePipesHub.ScenePipe());

            if (settings.SelfSending)
                SelfSendWithDelayAsync(emote, timestamp, mask).Forget();
        }

        public void OnPlayerRemoved(string walletId) =>
            messageScheduler.RemoveWallet(walletId);

        private void SendTo(URN emoteId, float timestamp, AvatarEmoteMask mask, IMessagePipe messagePipe)
        {
            MessageWrap<PlayerEmote> emote = messagePipe.NewMessage<PlayerEmote>();

            emote.Payload.IncrementalId = nextIncrementalId++;
            emote.Payload.Urn = emoteId;
            emote.Payload.Timestamp = timestamp;
            emote.Payload.Mask = (uint)mask;
            // Message objects are pooled/reused; ensure no stale optional fields leak from previous uses (e.g. SendStop()).
            emote.Payload.ClearIsStopping();
            emote.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }

        private async UniTaskVoid SelfSendWithDelayAsync(URN urn, float timestamp, AvatarEmoteMask mask)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(LATENCY), cancellationToken: cancellationTokenSource.Token);
            Inbox(RemotePlayerMovementComponent.TEST_ID, urn, timestamp, mask);
        }

        public void SendStop()
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("EmoteMessagesBus is disposed");

            float timestamp = Time.unscaledTime;

            SendStopTo(timestamp, messagePipesHub.IslandPipe());
            SendStopTo(timestamp, messagePipesHub.ScenePipe());

            if (settings.SelfSending)
                SelfSendStopWithDelayAsync(timestamp).Forget();
        }
        private void SendStopTo(float timestamp, IMessagePipe messagePipe)
        {
            MessageWrap<PlayerEmote> emote = messagePipe.NewMessage<PlayerEmote>();
            emote.Payload.IncrementalId = nextIncrementalId++;
            emote.Payload.Timestamp = timestamp;
            emote.Payload.IsStopping = true;
            emote.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }
        private async UniTaskVoid SelfSendStopWithDelayAsync(float timestamp)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(LATENCY), cancellationToken: cancellationTokenSource.Token);
            InboxStop(RemotePlayerMovementComponent.TEST_ID, timestamp);
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

                if (receivedMessage.Payload is { HasIsStopping: true, IsStopping: true })
                {
                    // TODO (Maurizio) remove after tests
                    Debug.Log($"(Maurizio) Requested to stop emoting on walletId: {receivedMessage.FromWalletId}");

                    InboxStop(receivedMessage.FromWalletId, timestamp);
                    return;
                }

                AvatarEmoteMask mask = EnumUtils.FromInt<AvatarEmoteMask>((int)receivedMessage.Payload.Mask);

                Inbox(receivedMessage.FromWalletId, receivedMessage.Payload.Urn, timestamp, mask);
            }
        }

        private bool IsUserBlocked(string userAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.UserIsBlocked(userAddress);

        private void Inbox(string walletId, URN emoteURN, float timestamp, AvatarEmoteMask mask)
        {
            if (messageScheduler.TryPass(walletId, timestamp) == false)
                return;

            using (sync.GetScope())
                emoteIntentions.Add(new RemoteEmoteIntention(emoteURN, walletId, timestamp, mask));
        }

        private void InboxStop(string walletId, float timestamp)
        {
            if (messageScheduler.TryPass(walletId, timestamp) == false)
                return;
            using (sync.GetScope())
                emoteStopIntentions.Add(new RemoteEmoteStopIntention(walletId, timestamp));
        }

        public void SaveForRetry(RemoteEmoteIntention intention) =>
            emoteIntentions.Add(intention);

        public void SaveForRetry(RemoteEmoteStopIntention stopIntention) =>
            emoteStopIntentions.Add(stopIntention);
    }
}
