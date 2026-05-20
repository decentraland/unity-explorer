using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
using DCL.SceneBannedUsers;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Owns the creation part of the Nearby audio-source lifecycle.
    ///     Creates audio source component for every avatar streamer in audible range
    ///     the system materializes the audio-source component for the resolver-picked <c>(walletId, CurrentSid)</c> pair when one does not yet exist.
    /// </summary>
    [UpdateInGroup(typeof(NearbyVoiceChatGroup))]
    [UpdateAfter(typeof(NearbyAudibleRangeSystem))]
    [UpdateBefore(typeof(NearbyAudioPositionSystem))]
    public partial class NearbyAudioBindingSystem : BaseUnityLoopSystem
    {
        private readonly INearbyAudioStreamRegistry registry;
        private readonly IUserBlockingCache userBlockingCache;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly INearbyAudioSourceFactory sourceFactory;
        private readonly RoomMetadataCurrentScene roomMetadataCurrentScene;


        internal NearbyAudioBindingSystem(World world, INearbyAudioStreamRegistry registry, IUserBlockingCache userBlockingCache, NearbyVoiceChatStateModel stateModel, INearbyAudioSourceFactory sourceFactory, RoomMetadataCurrentScene roomMetadataCurrentScene) : base(world)
        {
            this.registry = registry;
            this.userBlockingCache = userBlockingCache;
            this.stateModel = stateModel;
            this.sourceFactory = sourceFactory;
            this.roomMetadataCurrentScene = roomMetadataCurrentScene;
        }

        protected override void OnDispose()
        {
            sourceFactory.DisposeRoot();
        }

        protected override void Update(float t)
        {
            // Listening gate: skip the entire avatar query when nearby chat is SUPPRESSED or DISABLED.
            // Cleanup system handles the symmetric teardown of any already-bound entities.
            if (stateModel.IsListeningDisabled) return;

            CreateAndBindAudioSourcesToStreamersQuery(World);
        }

        [Query]
        [None(typeof(NearbyAudioSourceComponent), typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(InAudibleRangeTag))]
        private void CreateAndBindAudioSourcesToStreamers(Entity avatarEntity, in Profile profile, in NearbyAudioStreamerComponent nearby)
        {
            string walletId = profile.UserId;
            if (string.IsNullOrEmpty(walletId)) return;

            // Skip blocked / scene-banned identities. Cleanup system handles already-bound entities; this filter prevents creation in the first place.
            if (userBlockingCache.UserIsBlocked(walletId) || roomMetadataCurrentScene.IsUserBanned(walletId)) return;

            var key = new StreamKey(walletId, nearby.CurrentSid);
            Weak<AudioStream> stream = registry.GetActiveStream(key);

            if (stream.Resource.Has)
            {
                LivekitAudioSource source = sourceFactory.Create(key, stream);
                World.Add(avatarEntity, new NearbyAudioSourceComponent(key, source));
            }
        }
    }
}
