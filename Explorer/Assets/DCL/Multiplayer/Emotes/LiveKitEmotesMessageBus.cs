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
using Decentraland.Kernel.Comms.Rfc4;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Emotes
{
    public class LiveKitEmotesMessageBus : IDisposable, IEmotesMessageBus
    {
        private const float LATENCY = 0f;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly MultiplayerDebugSettings settings;
        private readonly IUserBlockingCache userBlockingCache;

        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly EmotesScheduler messageScheduler;
        private readonly LiveKitMessagesBroadcaster broadcaster;

        private readonly HashSet<RemoteEmoteIntention> emoteIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly HashSet<RemoteEmoteStopIntention> emoteStopIntentions = new (PoolConstants.AVATARS_COUNT);

        private readonly MutexSync sync = new();

        public LiveKitEmotesMessageBus(IMessagePipesHub messagePipesHub,
            MultiplayerDebugSettings settings,
            IUserBlockingCache userBlockingCache,
            LiveKitMessagesBroadcaster broadcaster)
        {
            this.messagePipesHub = messagePipesHub;
            this.settings = settings;
            this.userBlockingCache = userBlockingCache;
            this.broadcaster = broadcaster;

            messageScheduler = new EmotesScheduler();

            this.messagePipesHub.IslandPipe().Subscribe<PlayerEmote>(Packet.MessageOneofCase.PlayerEmote, OnMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<PlayerEmote>(Packet.MessageOneofCase.PlayerEmote, OnMessageReceived);
        }

        public void Dispose() =>
            cancellationTokenSource.SafeCancelAndDispose();

        public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() =>
            new (sync, emoteIntentions);

        public OwnedBunch<RemoteEmoteStopIntention> EmoteStopIntentions() =>
            new (sync, emoteStopIntentions);

        public void Send(URN emote, bool loopCyclePassed, AvatarEmoteMask mask, uint durationMs = 0, NetworkMovementMessage? playerState = null)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                return;

            float timestamp = Time.unscaledTime;

            broadcaster.Send(BUILD_PLAYER_EMOTE_MESSAGE, (emote, timestamp, mask), LKDataPacketKind.KindReliable, cancellationTokenSource.Token);

            if (settings.SelfSending)
                SelfSendWithDelayAsync(emote, timestamp, mask).Forget();
        }

        public void SendStop()
        {
            if (cancellationTokenSource.IsCancellationRequested)
                return;

            float timestamp = Time.unscaledTime;

            broadcaster.Send(BUILD_STOP_MESSAGE, timestamp, LKDataPacketKind.KindReliable, cancellationTokenSource.Token);

            if (settings.SelfSending)
                SelfSendStopWithDelayAsync(timestamp).Forget();
        }

        public void OnPlayerRemoved(string walletId) =>
            messageScheduler.RemoveWallet(walletId);

        private static readonly Action<(URN, float, AvatarEmoteMask), PlayerEmote> BUILD_PLAYER_EMOTE_MESSAGE = BuildPlayerEmoteMessage;

        private static void BuildPlayerEmoteMessage((URN emoteId, float timestamp, AvatarEmoteMask mask) input, PlayerEmote payload)
        {
            payload.Urn = input.emoteId;
            payload.Timestamp = input.timestamp;
            payload.Mask = (uint)input.mask;
            // Message objects are pooled/reused; ensure no stale optional fields leak from previous uses (e.g. SendStop()).
            payload.ClearIsStopping();
        }

        private async UniTaskVoid SelfSendWithDelayAsync(URN urn, float timestamp, AvatarEmoteMask mask)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(LATENCY), cancellationToken: cancellationTokenSource.Token);
            Inbox(RemotePlayerMovementComponent.TEST_ID, urn, timestamp, mask);
        }

        private static readonly Action<float, PlayerEmote> BUILD_STOP_MESSAGE = BuildStopMessage;

        private static void BuildStopMessage(float timestamp, PlayerEmote payload)
        {
            payload.Timestamp = timestamp;
            payload.IsStopping = true;
        }
        private async UniTaskVoid SelfSendStopWithDelayAsync(float timestamp)
        {
            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(LATENCY), cancellationToken: cancellationTokenSource.Token).SuppressCancellationThrow();
            if (cancelled) return;
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
                    InboxStop(receivedMessage.FromWalletId, timestamp);
                    return;
                }

                AvatarEmoteMask mask = EnumUtils.FromInt<AvatarEmoteMask>((int)receivedMessage.Payload.Mask);

                Inbox(receivedMessage.FromWalletId, receivedMessage.Payload.Urn, timestamp, mask);
            }
        }

        private bool IsUserBlocked(string userAddress) =>
            userBlockingCache.UserIsBlocked(userAddress);

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
