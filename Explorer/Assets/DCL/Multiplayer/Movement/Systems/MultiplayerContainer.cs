using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Friends.UserBlocking;
using DCL.Landscape.Settings;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Optimization.Multithreading;
using DCL.Optimization.Pools;
using DCL.PluginSystem;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public class MultiplayerContainer : IDisposable
    {
        private class MovementMessageBusProxy : IMovementMessageBus
        {
            private readonly PulseMultiplayerBus pulseMultiplayerBus;
            private readonly LiveKitMovementMessageBus liveKitMovementMessageBus;

            public MovementMessageBusProxy(PulseMultiplayerBus pulseMultiplayerBus, LiveKitMovementMessageBus liveKitMovementMessageBus)
            {
                this.pulseMultiplayerBus = pulseMultiplayerBus;
                this.liveKitMovementMessageBus = liveKitMovementMessageBus;
            }

            public void BroadcastTeleport(string realmName, Vector3 worldPosition)
            {
                pulseMultiplayerBus.BroadcastTeleport(realmName, worldPosition);
                liveKitMovementMessageBus.BroadcastTeleport(realmName, worldPosition);
            }

            public void Send(NetworkMovementMessage message)
            {
                pulseMultiplayerBus.Send(message);
                liveKitMovementMessageBus.Send(message);
            }
        }

        private class RemoteAnnouncementsProxy : IRemoteAnnouncements
        {
            private readonly PulseIncomingProfileAnnouncements pulseAnnouncements;
            private readonly LiveKitRemoteAnnouncements liveKitAnnouncements;

            public RemoteAnnouncementsProxy(PulseIncomingProfileAnnouncements pulseAnnouncements, LiveKitRemoteAnnouncements liveKitAnnouncements)
            {
                this.pulseAnnouncements = pulseAnnouncements;
                this.liveKitAnnouncements = liveKitAnnouncements;
            }

            public void Fill(List<RemoteAnnouncement> announcements)
            {
                pulseAnnouncements.Fill(announcements);
                liveKitAnnouncements.Fill(announcements);
            }

            public void Remove(IReadOnlyCollection<RemoveIntention> removeIntentions)
            {
                pulseAnnouncements.Remove(removeIntentions);
                liveKitAnnouncements.Remove(removeIntentions);
            }
        }

        private class EmoteMessageBusProxy : IEmotesMessageBus
        {
            private readonly PulseMultiplayerBus pulseMultiplayerBus;
            private readonly LiveKitEmotesMessageBus liveKitEmotesMessageBus;

            // Not used
            private readonly MutexSync mutexSync = new ();

            private readonly HashSet<RemoteEmoteIntention> emoteIntentions = new (PoolConstants.AVATARS_COUNT);
            private readonly HashSet<RemoteEmoteStopIntention> emoteStopIntentions = new (PoolConstants.AVATARS_COUNT);

            public EmoteMessageBusProxy(PulseMultiplayerBus pulseMultiplayerBus, LiveKitEmotesMessageBus liveKitEmotesMessageBus)
            {
                this.pulseMultiplayerBus = pulseMultiplayerBus;
                this.liveKitEmotesMessageBus = liveKitEmotesMessageBus;
            }

            public OwnedBunch<RemoteEmoteIntention> EmoteIntentions()
            {
                using OwnedBunch<RemoteEmoteIntention> pulseIntentions = pulseMultiplayerBus.EmoteIntentions();
                using OwnedBunch<RemoteEmoteIntention> liveKitIntentions = liveKitEmotesMessageBus.EmoteIntentions();

                emoteIntentions.UnionWith(pulseIntentions.Collection());
                emoteIntentions.UnionWith(liveKitIntentions.Collection());

                return new OwnedBunch<RemoteEmoteIntention>(mutexSync, emoteIntentions);
            }

            public OwnedBunch<RemoteEmoteStopIntention> EmoteStopIntentions()
            {
                using OwnedBunch<RemoteEmoteStopIntention> pulseIntentions = pulseMultiplayerBus.EmoteStopIntentions();
                using OwnedBunch<RemoteEmoteStopIntention> liveKitIntentions = liveKitEmotesMessageBus.EmoteStopIntentions();

                emoteStopIntentions.UnionWith(pulseIntentions.Collection());
                emoteStopIntentions.UnionWith(liveKitIntentions.Collection());

                return new OwnedBunch<RemoteEmoteStopIntention>(mutexSync, emoteStopIntentions);
            }

            public void Send(URN urn, bool loopCyclePassed, AvatarEmoteMask mask, uint durationMs = 0, NetworkMovementMessage? playerState = null)
            {
                pulseMultiplayerBus.Send(urn, loopCyclePassed, mask, durationMs, playerState);
                liveKitEmotesMessageBus.Send(urn, loopCyclePassed, mask, durationMs, playerState);
            }

            public void SendStop()
            {
                pulseMultiplayerBus.SendStop();
                liveKitEmotesMessageBus.SendStop();
            }

            public void OnPlayerRemoved(string walletId)
            {
                pulseMultiplayerBus.OnPlayerRemoved(walletId);
                liveKitEmotesMessageBus.OnPlayerRemoved(walletId);
            }

            public void SaveForRetry(RemoteEmoteIntention intention)
            {
                pulseMultiplayerBus.SaveForRetry(intention);
                liveKitEmotesMessageBus.SaveForRetry(intention);
            }

            public void SaveForRetry(RemoteEmoteStopIntention intention)
            {
                pulseMultiplayerBus.SaveForRetry(intention);
                liveKitEmotesMessageBus.SaveForRetry(intention);
            }
        }

        private class RemoveIntentionsProxy : IRemoveIntentions
        {
            private readonly MutexSync mutexSync = new ();
            private readonly HashSet<RemoveIntention> set = new ();

            private readonly PulseRemoveIntentions pulseRemoveIntentions;
            private readonly LiveKitRemoveIntentions liveKitRemoveIntentions;

            public RemoveIntentionsProxy(PulseRemoveIntentions pulseRemoveIntentions, LiveKitRemoveIntentions liveKitRemoveIntentions)
            {
                this.pulseRemoveIntentions = pulseRemoveIntentions;
                this.liveKitRemoveIntentions = liveKitRemoveIntentions;
            }

            public OwnedBunch<RemoveIntention> Bunch()
            {
                using OwnedBunch<RemoveIntention> pulse = pulseRemoveIntentions.Bunch();
                using OwnedBunch<RemoveIntention> livekit = liveKitRemoveIntentions.Bunch();

                set.UnionWith(pulse.Collection());
                set.UnionWith(livekit.Collection());

                return new OwnedBunch<RemoveIntention>(mutexSync, set);
            }
        }

        private readonly PulseContainer pulseContainer;
        private readonly LiveKitMultiplayerContainer liveKitContainer;
        private readonly ISelfProfile selfProfile;

        public readonly IMovementMessageBus MovementMessageBus;
        public readonly IRemoteAnnouncements RemoteAnnouncements;
        public readonly IEmotesMessageBus EmotesMessageBus;
        public readonly IRemoveIntentions RemoveIntentions;

        public IProfileBroadcast ProfileBroadcast => liveKitContainer.ProfileBroadcast;
        public IProfilePropagation ProfilePropagation => pulseContainer.pulseProfilePropagationBus!;
        public IPulseMultiplayerService PulseMultiplayerService => pulseContainer.pulseMultiplayerService!;
        public ParcelEncoder ParcelEncoder => pulseContainer.parcelEncoder;
        public LiveKitMovementMessageBus LiveKitMovementMessageBus => liveKitContainer.MovementMessageBus;
        public PulseMultiplayerBus PulseMultiplayerBus => pulseContainer.pulseMultiplayerBus!;
        public ITransport PulseTransport => pulseContainer.transport!;

        private MultiplayerContainer(PulseContainer pulseContainer, LiveKitMultiplayerContainer liveKitContainer, ISelfProfile selfProfile)
        {
            this.pulseContainer = pulseContainer;
            this.liveKitContainer = liveKitContainer;
            this.selfProfile = selfProfile;

            // Create Proxies to expose them to consumers
            MovementMessageBus = new MovementMessageBusProxy(pulseContainer.pulseMultiplayerBus!, liveKitContainer.MovementMessageBus);
            RemoteAnnouncements = new RemoteAnnouncementsProxy(pulseContainer.IncomingProfiles, liveKitContainer.RemoteAnnouncements);
            EmotesMessageBus = new EmoteMessageBusProxy(pulseContainer.pulseMultiplayerBus!, liveKitContainer.EmotesMessageBus);
            RemoveIntentions = new RemoveIntentionsProxy(pulseContainer.RemoveIntentions, liveKitContainer.RemoveIntentions);

            selfProfile.ProfilePropagated += OnSelfProfilePropagated;
        }

        public static async UniTask<MultiplayerContainer> CreateAsync(
            IPluginSettingsContainer pluginSettingsContainer,
            IWeb3IdentityCache identityCache,
            MovementInbox movementInbox,
            LandscapeData landscapeData,
            IDecentralandUrlsSource urlsSource,
            IRoomHub roomHub,
            IMessagePipesHub messagePipesHub,
            MultiplayerDebugSettings multiplayerDebugSettings,
            IUserBlockingCache userBlockingCache,
            ISelfProfile selfProfile,
            CancellationToken ct) =>
            new (
                await PulseContainer.CreateAsync(pluginSettingsContainer, identityCache, movementInbox, landscapeData, urlsSource, selfProfile, ct),
                new LiveKitMultiplayerContainer(roomHub, messagePipesHub, movementInbox, selfProfile, userBlockingCache, multiplayerDebugSettings),
                selfProfile
            );

        private void OnSelfProfilePropagated(Profile profile) =>
            ProfilePropagation.Propagate(profile);

        public void Dispose()
        {
            selfProfile.ProfilePropagated -= OnSelfProfilePropagated;
            pulseContainer.Dispose();
            liveKitContainer.Dispose();
        }
    }
}
