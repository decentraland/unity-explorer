using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
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

namespace DCL.Multiplayer.Emotes
{
    public class MultiplayerEmotesMessageBus : IDisposable, IEmotesMessageBus
    {
        private const float LATENCY = 0f;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly ProvidedAsset<MultiplayerDebugSettings> settings;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;

        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly EmotesScheduler messageScheduler;

        private readonly HashSet<RemoteEmoteIntention> emoteIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly MutexSync sync = new();

        public MultiplayerEmotesMessageBus(IMessagePipesHub messagePipesHub,
            ProvidedAsset<MultiplayerDebugSettings> settings,
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

        public void Send(URN emote, bool loopCyclePassed)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("EmoteMessagesBus is disposed");

            float timestamp = UnityEngine.Time.unscaledTime;

            SendTo(emote, timestamp, messagePipesHub.IslandPipe());
            SendTo(emote, timestamp, messagePipesHub.ScenePipe());

            if (settings.Value.SelfSending)
                SelfSendWithDelayAsync(emote, timestamp).Forget();
        }

        public void OnPlayerRemoved(string walletId) =>
            messageScheduler.RemoveWallet(walletId);

        private void SendTo(URN emoteId, float timestamp, IMessagePipe messagePipe)
        {
            MessageWrap<PlayerEmote> emote = messagePipe.NewMessage<PlayerEmote>();

            emote.Payload.Urn = emoteId;
            emote.Payload.Timestamp = timestamp;
            emote.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }

        private async UniTaskVoid SelfSendWithDelayAsync(URN urn, float timestamp)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(LATENCY), cancellationToken: cancellationTokenSource.Token);
            Inbox(RemotePlayerMovementComponent.TEST_ID, urn, timestamp);
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

                Inbox(receivedMessage.FromWalletId, receivedMessage.Payload.Urn, receivedMessage.Payload.Timestamp);
            }
        }

        private bool IsUserBlocked(string userAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.UserIsBlocked(userAddress);

        private void Inbox(string walletId, URN emoteURN, float timestamp)
        {
            if (messageScheduler.TryPass(walletId, timestamp) == false)
                return;

            using (sync.GetScope())
                emoteIntentions.Add(new RemoteEmoteIntention(emoteURN, walletId, timestamp));
        }

        public void SaveForRetry(RemoteEmoteIntention intention)
        {
            emoteIntentions.Add(intention);
        }
    }
}
