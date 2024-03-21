using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Profiles;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Proto;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Emotes
{
    public class MultiplayerEmotesMessageBus: IDisposable
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IProfileRepository profileRepository;

        private readonly CancellationTokenSource cancellationTokenSource = new ();

        private World globalWorld = null!;

        // private readonly IMessagePipesHub messagePipesHub;
        // private readonly IProfileRepository profileRepository;
        // private readonly CancellationTokenSource cancellationTokenSource = new ();

        public MultiplayerEmotesMessageBus(IMessagePipesHub messagePipesHub, IReadOnlyEntityParticipantTable entityParticipantTable, IProfileRepository profileRepository)
        {
            this.messagePipesHub = messagePipesHub;
            this.entityParticipantTable = entityParticipantTable;
            this.profileRepository = profileRepository;

            this.messagePipesHub.IslandPipe().Subscribe<Emote>(Packet.MessageOneofCase.Emote, OnMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<Emote>(Packet.MessageOneofCase.Emote, OnMessageReceived);
        }

        public void InjectWorld(World world)
        {
            this.globalWorld = world;
        }
        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public void Send(uint emote)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("EmoteMessagesBus is disposed");

            float timestamp = UnityEngine.Time.unscaledTime;

            SendTo(emote, timestamp, messagePipesHub.IslandPipe());
            SendTo(emote, timestamp, messagePipesHub.ScenePipe());
        }

        private void SendTo(uint emoteId, float timestamp, IMessagePipe messagePipe)
        {
            var emote = messagePipe.NewMessage<Emote>();

            emote.Payload.EmoteId = emoteId;
            emote.Payload.Timestamp = timestamp;
            emote.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();
        }

        public async UniTaskVoid SelfSendWithDelayAsync(Emote message, float delay)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationTokenSource.Token);
            Inbox(message, walletId: RemotePlayerMovementComponent.TEST_ID).Forget();
        }

        private void OnMessageReceived(ReceivedMessage<Emote> obj)
        {
            if (cancellationTokenSource.Token.IsCancellationRequested)
                return;

            Inbox(obj.Payload, obj.FromWalletId).Forget();
        }

        private async UniTaskVoid Inbox(Emote emoteMessage, string walletId)
        {
            if (entityParticipantTable.Has(walletId) == false)
            {
                // ReportHub.LogWarning(ReportCategory.MULTIPLAYER_MOVEMENT, $"Entity for wallet {walletId} not found");
                return;
            }
            // if (messageDeduplication.TryPass(receivedMessage.FromWalletId, receivedMessage.Payload.Timestamp) == false)
            //     return;

            var entity = entityParticipantTable.Entity(walletId);
            Profile? profile = await profileRepository.GetAsync(walletId, 0, cancellationTokenSource.Token);
            TriggerEmote((int)emoteMessage.EmoteId, entity, profile!);
            // ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"Movement from {@for} - {fullMovementMessage}");
        }

        // Copy-Paste from UpdateEmoteInputSystem.cs
        private void TriggerEmote(int emoteIndex, in Entity entity, in Profile profile)
        {
            URN emoteId;

            if(profile != null)
            {
                IReadOnlyList<URN> emotes = profile.Avatar.Emotes;

                if (emoteIndex < 0 || emoteIndex >= emotes.Count)
                    return;

                emoteId = emotes[emoteIndex].Shorten();
            }
            else
            {
                emoteId = new URN("cry");
            }

            if (!string.IsNullOrEmpty(emoteId))
            {
                globalWorld.Add(entity, new CharacterEmoteIntent { EmoteId = emoteId });
                globalWorld.Get<CharacterAnimationComponent>(entity).States.WasEmoteJustTriggered = true;
            }
        }
    }
}
