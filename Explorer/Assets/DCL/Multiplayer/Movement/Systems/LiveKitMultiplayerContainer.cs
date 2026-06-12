using DCL.FeatureFlags;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.BroadcastProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Profiles.Self;
using DCL.Utilities;
using System;

namespace DCL.Multiplayer.Movement
{
    internal class LiveKitMultiplayerContainer : IDisposable
    {
        public readonly LiveKitMovementMessageBus MovementMessageBus;
        public readonly LiveKitRemoteAnnouncements RemoteAnnouncements;
        public readonly LiveKitEmotesMessageBus EmotesMessageBus;
        public readonly DebounceLiveKitProfileBroadcast ProfileBroadcast;
        public readonly LiveKitRemoveIntentions RemoveIntentions;

        internal LiveKitMultiplayerContainer(
            IRoomHub roomHub,
            IMessagePipesHub messagePipesHub,
            MovementInbox movementInbox,
            ISelfProfile selfProfile,
            IUserBlockingCache userBlockingCache,
            MultiplayerDebugSettings multiplayerDebugSettings)
        {
            bool backwardCompatibilityMode = FeaturesRegistry.Instance.IsEnabled(FeatureId.PULSE);
            var broadcaster = new LiveKitMessagesBroadcaster(messagePipesHub, backwardCompatibilityMode);

            RemoteAnnouncements = new LiveKitRemoteAnnouncements(messagePipesHub, broadcaster);
            ProfileBroadcast = new DebounceLiveKitProfileBroadcast(new LiveKitProfileBroadcast(selfProfile, broadcaster));
            MovementMessageBus = new LiveKitMovementMessageBus(messagePipesHub, movementInbox, broadcaster);
            EmotesMessageBus = new LiveKitEmotesMessageBus(messagePipesHub, multiplayerDebugSettings, userBlockingCache, broadcaster);
            RemoveIntentions = new LiveKitRemoveIntentions(roomHub);
        }

        public void Dispose()
        {
            MovementMessageBus.Dispose();
            EmotesMessageBus.Dispose();
        }
    }
}
