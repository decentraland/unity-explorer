using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Optimization.Multithreading;
using DCL.Optimization.Pools;
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

        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly EmotesDeduplication messageDeduplication;
        private EmoteSendIdProvider sendIdProvider;

        private readonly HashSet<RemoteEmoteIntention> emoteIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly MutexSync sync = new();

        public MultiplayerEmotesMessageBus(IMessagePipesHub messagePipesHub, ProvidedAsset<MultiplayerDebugSettings> settings)
        {
            this.messagePipesHub = messagePipesHub;
            this.settings = settings;

            messageDeduplication = new EmotesDeduplication();

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

            uint sendId = sendIdProvider.GetNextID(emote, loopCyclePassed);

            SendTo(emote, sendId, messagePipesHub.IslandPipe());
            SendTo(emote, sendId, messagePipesHub.ScenePipe());

            if (settings.Value.SelfSending)
                SelfSendWithDelayAsync(emote, sendId).Forget();
        }

        public void OnPlayerRemoved(string walletId) =>
            messageDeduplication.RemoveWallet(walletId);

        private void SendTo(URN emoteId, uint timestamp, IMessagePipe messagePipe)
        {
            MessageWrap<PlayerEmote> emote = messagePipe.NewMessage<PlayerEmote>();

            emote.Payload.Urn = emoteId;
            emote.Payload.IncrementalId = timestamp;
            emote.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }

        private async UniTaskVoid SelfSendWithDelayAsync(URN urn, uint id)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(LATENCY), cancellationToken: cancellationTokenSource.Token);
            Inbox(RemotePlayerMovementComponent.TEST_ID, urn, id);
        }

        private void OnMessageReceived(ReceivedMessage<PlayerEmote> receivedMessage)
        {
            using (receivedMessage)
            {
                if (cancellationTokenSource.Token.IsCancellationRequested)
                    return;

                Inbox(receivedMessage.FromWalletId, receivedMessage.Payload.Urn, receivedMessage.Payload.IncrementalId);
            }
        }

        private void Inbox(string walletId, URN emoteURN, uint incrementalId)
        {
            if (messageDeduplication.TryPass(walletId, incrementalId) == false)
                return;

            using (sync.GetScope())
                emoteIntentions.Add(new RemoteEmoteIntention(emoteURN, walletId));
        }

        public void SaveForRetry(RemoteEmoteIntention intention)
        {
            emoteIntentions.Add(intention);
        }
    }
}
