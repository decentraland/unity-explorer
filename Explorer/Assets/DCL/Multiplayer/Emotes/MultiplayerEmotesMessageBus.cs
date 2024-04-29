using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Emotes.Interfaces;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using System;
using System.Threading;

namespace DCL.Multiplayer.Emotes
{
    public class MultiplayerEmotesMessageBus : IDisposable, IEmotesMessageBus
    {
        private const float LATENCY = 0.1f;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IWeb3IdentityCache identityCache;

        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly EmotesDeduplication messageDeduplication;
        private EmoteSendIdProvider sendIdProvider;

        private World globalWorld = null!;

        private Entity playerEntity;

        public MultiplayerEmotesMessageBus(IMessagePipesHub messagePipesHub, IReadOnlyEntityParticipantTable entityParticipantTable, IWeb3IdentityCache identityCache)
        {
            this.messagePipesHub = messagePipesHub;
            this.entityParticipantTable = entityParticipantTable;
            this.identityCache = identityCache;

            messageDeduplication = new EmotesDeduplication();

            this.messagePipesHub.IslandPipe().Subscribe<PlayerEmote>(Packet.MessageOneofCase.PlayerEmote, OnMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<PlayerEmote>(Packet.MessageOneofCase.PlayerEmote, OnMessageReceived);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public void InjectWorld(World world, Entity playerEntity)
        {
            globalWorld = world;
            this.playerEntity = playerEntity;
        }

        public void Send(URN emote, bool loopCyclePassed, bool sendToSelfReplica)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("EmoteMessagesBus is disposed");

            uint sendId = sendIdProvider.GetNextID(emote, loopCyclePassed);

            SendTo(emote, sendId, messagePipesHub.IslandPipe());
            SendTo(emote, sendId, messagePipesHub.ScenePipe());

            if (sendToSelfReplica)
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

            var entity = EntityOrNull(walletId);

            if (entity == Entity.Null)
            {
                ReportHub.LogWarning(ReportCategory.EMOTE, $"Cannot find entity for walletId: {walletId}");
                return;
            }

            TriggerEmote(emoteURN, entity);
        }

        private Entity EntityOrNull(string walletId)
        {
            if (identityCache.Identity!.Address.Equals(walletId))
                return playerEntity;

            if (entityParticipantTable.Has(walletId))
                return entityParticipantTable.Entity(walletId);

            return Entity.Null;
        }

        private void TriggerEmote(URN emoteURN, in Entity entity)
        {
            if (!emoteURN.IsNullOrEmpty())
                globalWorld.Add(entity, new CharacterEmoteIntent { EmoteId = emoteURN });
        }
    }
}
