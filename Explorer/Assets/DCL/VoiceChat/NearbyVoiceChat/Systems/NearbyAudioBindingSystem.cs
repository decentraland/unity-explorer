using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
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
    ///     the system iterates the snapshotted sids on the entity itself and materializes an audio-source entity per <c>(walletId, sid)</c> pair that does not yet have one.
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


        internal NearbyAudioBindingSystem(World world, INearbyAudioStreamRegistry registry, HashSet<StreamKey> bindings, IUserBlockingCache userBlockingCache, NearbyVoiceChatStateModel stateModel, INearbyAudioSourceFactory sourceFactory) : base(world)
        {
            this.registry = registry;
            this.bindings = bindings;
            this.userBlockingCache = userBlockingCache;
            this.stateModel = stateModel;
            this.sourceFactory = sourceFactory;
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

            // Skip blocked identities. Cleanup system handles already-bound entities; this filter prevents creation in the first place.
            if (userBlockingCache.UserIsBlocked(walletId)) return;

            // Sids ride on the entity itself — no registry call on the per-avatar hot path.
            // Bridge system guarantees SidsSnapshot is non-null for any entity that has the component.
            foreach (string sid in nearby.StreamSidsSnapshot)
            {
                var key = new StreamKey(walletId, sid);

                if (!bindings.Contains(key))
                {
                    Weak<AudioStream> stream = registry.GetActiveStream(key);

                    // Track was unsubscribed between collection (snapshot read) and resolve (GetActiveStream); skip to avoid a one-frame ghost source.
                    if (!stream.Resource.Has) continue;

                    LivekitAudioSource source = sourceFactory.Create(key, stream);

                    World.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
                    bindings.Add(key);
                }
            }
        }
    }
}
