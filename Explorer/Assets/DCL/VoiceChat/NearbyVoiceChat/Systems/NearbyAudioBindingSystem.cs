using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Owns the creation part of the Nearby audio-source lifecycle.
    ///     For every avatar entity (<see cref="Profile"/> + <see cref="AvatarBase"/>) the system iterates the registry's
    ///     <c>(identity → sids)</c> snapshot and materializes an audio-source entity per <c>(walletId, sid)</c> pair that
    ///     does not yet have one. Throttled to a fixed budget per frame so a crowd ramp-up does not spike a single tick.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class NearbyAudioBindingSystem : BaseUnityLoopSystem
    {
        internal const int MAX_CREATIONS_PER_FRAME = 10;

        private readonly INearbyAudioStreamRegistry registry;
        private readonly Dictionary<StreamKey, Entity> bindings;
        private readonly IUserBlockingCache userBlockingCache;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly NearbyAudioSourceFactory sourceFactory;

        private readonly List<(Entity avatarEntity, StreamKey key)> pendingCreations = new (16);

        internal NearbyAudioBindingSystem(World world, INearbyAudioStreamRegistry registry, Dictionary<StreamKey, Entity> bindings, IUserBlockingCache userBlockingCache, NearbyVoiceChatStateModel stateModel, NearbyAudioSourceFactory sourceFactory) : base(world)
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
            pendingCreations.Clear();

            // Listening gate: skip the entire avatar query when nearby chat is SUPPRESSED or DISABLED.
            // Cleanup system handles the symmetric teardown of any already-bound entities.
            if (stateModel.IsListeningDisabled) return;

            CollectPendingCreationsQuery(World);
            DrainPendingCreations();
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void CollectPendingCreations(Entity avatarEntity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (string.IsNullOrEmpty(walletId)) return;

            // Skip blocked identities before allocating into pendingCreations — covers both
            // "I block them" and "they block me" via UserBlockingCache. Cleanup system handles
            // already-bound entities; this filter prevents creation in the first place.
            if (userBlockingCache.UserIsBlocked(walletId)) return;

            ConcurrentDictionary<string, byte>? sids = registry.GetAudioSids(walletId);
            if (sids == null) return;

            foreach (KeyValuePair<string, byte> entry in sids)
            {
                var key = new StreamKey(walletId, entry.Key);
                if (bindings.ContainsKey(key)) continue;

                pendingCreations.Add((avatarEntity, key));
            }
        }

        private void DrainPendingCreations()
        {
            int budget = MAX_CREATIONS_PER_FRAME;

            foreach ((Entity avatarEntity, StreamKey key) in pendingCreations)
            {
                if (budget <= 0) break;
                if (bindings.ContainsKey(key)) continue;

                Weak<AudioStream> stream = registry.GetActiveStream(key);

                // Race on spawn: the track was unsubscribed between GetAudioSids (collection pass) and GetActiveStream
                // (resolve step). Skipping here avoids a one-frame ghost LivekitAudioSource that the cleanup system
                // would otherwise reap on the next tick — see PRD-cleanup §"race-on-spawn guard".
                if (!stream.Resource.Has) continue;

                LivekitAudioSource source = sourceFactory.Create(key, stream);

                Entity audioEntity = World.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
                bindings.Add(key, audioEntity);
                budget--;
            }
        }
    }
}
