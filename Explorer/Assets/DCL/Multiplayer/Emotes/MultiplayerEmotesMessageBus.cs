using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes.Components;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Emotes.Interfaces;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Profiles;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Emote = Decentraland.Kernel.Comms.Rfc4.Emote;

namespace DCL.Multiplayer.Emotes
{
    public class MultiplayerEmotesMessageBus : IDisposable, IEmotesMessageBus
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IProfileRepository profileRepository;

        private readonly CancellationTokenSource cancellationTokenSource = new ();

        private World globalWorld = null!;

        private Profile? selfProfile;
        private Entity playerEntity;

        public MultiplayerEmotesMessageBus(IMessagePipesHub messagePipesHub, IReadOnlyEntityParticipantTable entityParticipantTable, IProfileRepository profileRepository)
        {
            this.messagePipesHub = messagePipesHub;
            this.entityParticipantTable = entityParticipantTable;
            this.profileRepository = profileRepository;

            this.messagePipesHub.IslandPipe().Subscribe<Emote>(Packet.MessageOneofCase.Emote, OnMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<Emote>(Packet.MessageOneofCase.Emote, OnMessageReceived);
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public void InjectWorld(World world)
        {
            globalWorld = world;
        }

        public void Send(uint emote)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("EmoteMessagesBus is disposed");

            float timestamp = Time.unscaledTime;

            SendTo(emote, timestamp, messagePipesHub.IslandPipe());
            SendTo(emote, timestamp, messagePipesHub.ScenePipe());
        }

        private void SendTo(uint emoteId, float timestamp, IMessagePipe messagePipe)
        {
            MessageWrap<Emote> emote = messagePipe.NewMessage<Emote>();

            emote.Payload.EmoteId = emoteId;
            emote.Payload.Timestamp = timestamp;
            emote.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }

        public async UniTaskVoid SelfSendWithDelayAsync(Emote message, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationTokenSource.Token);
            Inbox(message, walletId: RemotePlayerMovementComponent.TEST_ID).Forget();
        }

        public void SetOwnProfile(Entity playerEntity) =>
            this.playerEntity = playerEntity;

        private void OnMessageReceived(ReceivedMessage<Emote> obj)
        {
            if (cancellationTokenSource.Token.IsCancellationRequested)
                return;

            Inbox(obj.Payload, obj.FromWalletId).Forget();
        }

        private async UniTaskVoid Inbox(Emote emoteMessage, string walletId)
        {
            // if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false)
            //     return;

            Profile? profile = await profileRepository.GetAsync(walletId, 0, cancellationTokenSource.Token);

            if (profile == null)
                profile = selfProfile ?? globalWorld.Get<Profile>(playerEntity);

            Entity entity = entityParticipantTable.Entity(walletId);
            TriggerEmote((int)emoteMessage.EmoteId, entity, profile!);
        }

        // Copy-Paste from UpdateEmoteInputSystem.cs
        private void TriggerEmote(int emoteIndex, in Entity entity, in Profile profile)
        {
            IReadOnlyList<URN> emotes = profile.Avatar.Emotes;

            if (emoteIndex < 0 || emoteIndex >= emotes.Count)
                return;

            URN emoteId = emotes[emoteIndex].Shorten();

            if (!string.IsNullOrEmpty(emoteId))
                globalWorld.Add(entity, new CharacterEmoteIntent { EmoteId = emoteId });
        }
    }
}
