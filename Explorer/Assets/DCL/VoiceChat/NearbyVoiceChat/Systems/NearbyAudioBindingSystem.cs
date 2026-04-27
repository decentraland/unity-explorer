using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.Utilities.Extensions;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

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
        private readonly VoiceChatConfiguration configuration;

        private readonly Dictionary<StreamKey, Entity> bindings = new (32);
        private readonly List<(Entity avatarEntity, StreamKey key)> pendingCreations = new (16);

        private readonly Transform sourcesRoot;

        internal NearbyAudioBindingSystem(World world, INearbyAudioStreamRegistry registry, VoiceChatConfiguration configuration) : base(world)
        {
            this.registry = registry;
            this.configuration = configuration;

            sourcesRoot = new GameObject("VoiceChatSources_Nearby").transform;
        }

        protected override void Update(float t)
        {
            pendingCreations.Clear();
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
                LivekitAudioSource source = CreateAndPlaySource(key, stream);

                Entity audioEntity = World.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
                bindings[key] = audioEntity;
                budget--;
            }
        }

        private LivekitAudioSource CreateAndPlaySource(StreamKey key, Weak<AudioStream> stream)
        {
            LivekitAudioSource lkSource = LivekitAudioSource.New(true, isSpatial: true);
            lkSource.Construct(stream);

            AudioMixerGroup? mixerGroup = configuration.ChatAudioMixerGroup;
            AudioSource audioSource = lkSource.AudioSource.EnsureNotNull();
            audioSource.outputAudioMixerGroup = mixerGroup;
            audioSource.Apply3dAudioSettings(configuration.NearbyCustomRolloffCurve);
            lkSource.ApplySpatialSettings(configuration);

            lkSource.name = $"LivekitSource_{key.identity}";
            lkSource.transform.SetParent(sourcesRoot);

            // Start muted — NearbyAudioPositionSystem unmutes after first position sync to avoid an audio burst at world origin.
            audioSource.mute = true;
            lkSource.Play();

            return lkSource;
        }
    }
}
