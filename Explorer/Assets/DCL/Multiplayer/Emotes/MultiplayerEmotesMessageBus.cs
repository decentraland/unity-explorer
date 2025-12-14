using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
        private readonly HashSet<LookAtPositionIntention> lookAtPositionIntentions = new (PoolConstants.AVATARS_COUNT);
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

            this.messagePipesHub.IslandPipe().Subscribe<LookAtPosition>(Packet.MessageOneofCase.LookAtPosition, OnLookAtPositionMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<LookAtPosition>(Packet.MessageOneofCase.LookAtPosition, OnLookAtPositionMessageReceived);
        }

        private void OnLookAtPositionMessageReceived(ReceivedMessage<LookAtPosition> lookAtPositionMessage)
        {
            ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"MultiplayerEmotesMessageBus.OnLookAtPositionMessageReceived() <color=green>RECEIVED Look at: {new Vector3(lookAtPositionMessage.Payload.PositionX, lookAtPositionMessage.Payload.PositionY, lookAtPositionMessage.Payload.PositionZ).ToString("F6")} ADDRESS: {lookAtPositionMessage.Payload.TargetAvatarWalletAddress}</color>");
            lookAtPositionIntentions.Add(new LookAtPositionIntention(lookAtPositionMessage.Payload.TargetAvatarWalletAddress, new Vector3(lookAtPositionMessage.Payload.PositionX, lookAtPositionMessage.Payload.PositionY, lookAtPositionMessage.Payload.PositionZ)));
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() =>
            new (sync, emoteIntentions);

        public OwnedBunch<LookAtPositionIntention> LookAtPositionIntentions()
        {
            return new OwnedBunch<LookAtPositionIntention> (sync, lookAtPositionIntentions);
        }

        public void SendLookAtPositionMessage(string walletAddress, float worldPositionX, float worldPositionY, float worldPositionZ)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("EmoteMessagesBus is disposed");

            float timestamp = Time.unscaledTime;

            var lookAtPositionMessageIsland = messagePipesHub.IslandPipe().NewMessage<LookAtPosition>();
            lookAtPositionMessageIsland.Payload.Timestamp = timestamp;
            lookAtPositionMessageIsland.Payload.PositionX = worldPositionX;
            lookAtPositionMessageIsland.Payload.PositionY = worldPositionY;
            lookAtPositionMessageIsland.Payload.PositionZ = worldPositionZ;
            lookAtPositionMessageIsland.Payload.TargetAvatarWalletAddress = walletAddress;
            lookAtPositionMessageIsland.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();

            var lookAtPositionMessageScene = messagePipesHub.ScenePipe().NewMessage<LookAtPosition>();
            lookAtPositionMessageScene.Payload.Timestamp = timestamp;
            lookAtPositionMessageScene.Payload.PositionX = worldPositionX;
            lookAtPositionMessageScene.Payload.PositionY = worldPositionY;
            lookAtPositionMessageScene.Payload.PositionZ = worldPositionZ;
            lookAtPositionMessageScene.Payload.TargetAvatarWalletAddress = walletAddress;
            lookAtPositionMessageScene.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();

            ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"MultiplayerEmotesMessageBus.SendLookAtPositionMessage() <color=green>SENT LOOK AT POSITION: {new Vector3(worldPositionX, worldPositionY, worldPositionZ).ToString("F6")} ADDRESS: {walletAddress}</color>");
        }

        public void Send(URN emote, bool isRepeating, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress, string targetAvatarWalletAddress, bool isStopping, int interactionId)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                throw new Exception("EmoteMessagesBus is disposed");

            float timestamp = Time.unscaledTime;

            SendTo(emote, timestamp, messagePipesHub.IslandPipe(), socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress, targetAvatarWalletAddress, isStopping, isRepeating, interactionId);
            SendTo(emote, timestamp, messagePipesHub.ScenePipe(), socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress, targetAvatarWalletAddress, isStopping, isRepeating, interactionId);

            if (settings.SelfSending)
                SelfSendWithDelayAsync(emote, timestamp, isUsingSocialEmoteOutcome, socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress, targetAvatarWalletAddress, isStopping, isRepeating, interactionId).Forget();
        }

        public void OnPlayerRemoved(string walletId) =>
            messageScheduler.RemoveWallet(walletId);

        private void SendTo(URN emoteId, float timestamp, IMessagePipe messagePipe, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress, string targetAvatarWalletAddress, bool isStopping, bool isRepeating, int interactionId)
        {
            MessageWrap<PlayerEmote> emote = messagePipe.NewMessage<PlayerEmote>();

            emote.Payload.IncrementalId = nextIncrementalId++;
            emote.Payload.Urn = emoteId;
            emote.Payload.Timestamp = timestamp;
            emote.Payload.SocialEmoteOutcome = socialEmoteOutcomeIndex;
            emote.Payload.IsReacting = isReactingToSocialEmote;
            emote.Payload.SocialEmoteInitiator = socialEmoteInitiatorWalletAddress ?? string.Empty;
            emote.Payload.TargetAvatar = targetAvatarWalletAddress ?? string.Empty;
            emote.Payload.IsStopping = isStopping;
            emote.Payload.IsRepeating = isRepeating;
            emote.Payload.InteractionId = interactionId;
            emote.SendAndDisposeAsync(cancellationTokenSource.Token, DataPacketKind.KindReliable).Forget();

            ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"MultiplayerEmotesMessageBus.SendTo() <color=green>SENT TO [{emote.Payload.IncrementalId}]: {emote.Payload.Urn} reacting? {emote.Payload.IsReacting} initiator: {emote.Payload.SocialEmoteInitiator} isStopping: {isStopping} isRepeating: {isRepeating} interactionId: {interactionId} target: {targetAvatarWalletAddress}</color>");
        }

        private async UniTaskVoid SelfSendWithDelayAsync(URN urn, float timestamp, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress, string targetAvatarWalletAddress, bool isStopping, bool isRepeating, int interactionId)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(LATENCY), cancellationToken: cancellationTokenSource.Token);
            Inbox(RemotePlayerMovementComponent.TEST_ID, urn, timestamp, isUsingSocialEmoteOutcome, socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress, targetAvatarWalletAddress, isStopping, isRepeating, interactionId);
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

                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"MultiplayerEmotesMessageBus.OnMessageReceived() <color=green>RECEIVED [{receivedMessage.Payload.IncrementalId}]</color>");

                Inbox(receivedMessage.FromWalletId, receivedMessage.Payload.Urn, timestamp, receivedMessage.Payload.SocialEmoteOutcome > -1, receivedMessage.Payload.SocialEmoteOutcome, receivedMessage.Payload.IsReacting, receivedMessage.Payload.SocialEmoteInitiator, receivedMessage.Payload.TargetAvatar, receivedMessage.Payload.IsStopping, receivedMessage.Payload.IsRepeating, receivedMessage.Payload.InteractionId);
            }
        }

        private bool IsUserBlocked(string userAddress) =>
            userBlockingCacheProxy.Configured && userBlockingCacheProxy.Object!.UserIsBlocked(userAddress);

        private void Inbox(string walletId, URN emoteURN, float timestamp, bool isUsingSocialEmoteOutcome, int socialEmoteOutcomeIndex, bool isReactingToSocialEmote, string socialEmoteInitiatorWalletAddress, string targetAvatarWalletAddress, bool isStopping, bool isRepeating, int interactionId)
        {
            if (messageScheduler.TryPass(walletId, timestamp) == false)
                return;

            using (sync.GetScope())
            {
                ReportHub.Log(ReportCategory.SOCIAL_EMOTE, $"MultiplayerEmotesMessageBus.Inbox() INBOX: {walletId} {emoteURN} stop? {isStopping} is outcome? {isUsingSocialEmoteOutcome} reacting? {isReactingToSocialEmote} outcome index: {socialEmoteOutcomeIndex} initiator: {socialEmoteInitiatorWalletAddress} isRepeating: {isRepeating} interactionId: {interactionId} target: {targetAvatarWalletAddress}");
                emoteIntentions.Add(new RemoteEmoteIntention(emoteURN, walletId, timestamp, isUsingSocialEmoteOutcome, socialEmoteOutcomeIndex, isReactingToSocialEmote, socialEmoteInitiatorWalletAddress, targetAvatarWalletAddress, isStopping, isRepeating, interactionId));
            }
        }

        public void SaveForRetry(RemoteEmoteIntention intention)
        {
            emoteIntentions.Add(intention);
        }

        public void SaveForRetry(LookAtPositionIntention intention)
        {
            lookAtPositionIntentions.Add(intention);
        }
    }
}
