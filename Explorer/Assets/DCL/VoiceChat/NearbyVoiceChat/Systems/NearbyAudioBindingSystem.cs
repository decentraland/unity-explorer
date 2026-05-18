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
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Owns the creation part of the Nearby audio-source lifecycle.
    ///     For every avatar entity (<see cref="Profile"/> + <see cref="AvatarBase"/> + <see cref="NearbyAudioStreamerComponent"/> + <see cref="InAudibleRangeTag"/>)
    ///     the system materializes a single audio-source entity for the resolver-picked <c>(walletId, CurrentSid)</c> pair when one does not yet exist.
    ///     Throttled to a fixed budget per frame so a crowd ramp-up does not spike a single tick.
    /// </summary>
    [UpdateInGroup(typeof(NearbyVoiceChatGroup))]
    [UpdateAfter(typeof(NearbyAudibleRangeSystem))]
    [UpdateBefore(typeof(NearbyAudioPositionSystem))]
    public partial class NearbyAudioBindingSystem : BaseUnityLoopSystem
    {
        internal const int MAX_CREATIONS_PER_FRAME = 10;

        private readonly INearbyAudioStreamRegistry registry;
        private readonly HashSet<StreamKey> bindings;
        private readonly IUserBlockingCache userBlockingCache;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly INearbyAudioSourceFactory sourceFactory;
        private readonly RoomMetadataCurrentScene roomMetadataCurrentScene;


        internal NearbyAudioBindingSystem(World world, INearbyAudioStreamRegistry registry, HashSet<StreamKey> bindings, IUserBlockingCache userBlockingCache, NearbyVoiceChatStateModel stateModel, INearbyAudioSourceFactory sourceFactory, RoomMetadataCurrentScene roomMetadataCurrentScene) : base(world)
        {
            this.registry = registry;
            this.bindings = bindings;
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
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(NearbyAudioStreamerComponent), typeof(InAudibleRangeTag))]
        private void CreateAndBindAudioSourcesToStreamers(Entity avatarEntity, in Profile profile, in NearbyAudioStreamerComponent nearby)
        {
            string walletId = profile.UserId;
            if (string.IsNullOrEmpty(walletId)) return;

            // Skip blocked / scene-banned identities. Cleanup system handles already-bound entities; this filter prevents creation in the first place.
            if (userBlockingCache.UserIsBlocked(walletId) || roomMetadataCurrentScene.IsUserBanned(walletId)) return;

            // The resolver dedup contract guarantees one active sid per participant — bridge keeps CurrentSid
            // in sync with the registry's pick. Iterate it directly; no per-avatar registry call on the hot path.
            var key = new StreamKey(walletId, nearby.CurrentSid);

            if (bindings.Contains(key)) return;

            Weak<AudioStream> stream = registry.GetActiveStream(key);

            // Track was unsubscribed between bridge tick and resolve (GetActiveStream); skip to avoid a one-frame ghost source.
            if (!stream.Resource.Has) return;

            LivekitAudioSource source = sourceFactory.Create(key, stream);

            World.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
            bindings.Add(key);
        }
    }
}
