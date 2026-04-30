using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Pull-mirror from <see cref="INearbyAudioStreamRegistry"/> to avatar-entity archetype
    ///     state. Runs every tick in <see cref="AvatarGroup"/>, before
    ///     <see cref="NearbyAudioBindingSystem"/>. Stateless — pass-through under the listening
    ///     gate (state reflects LiveKit, not nearby-chat policy; consumers gate on policy).
    ///     <para>
    ///         Carries the per-wallet sid set onto the avatar via <see cref="StreamingAudioComponent"/>.
    ///         The freshness check is a single <see cref="object.ReferenceEquals(object, object)"/>
    ///         against the registry's copy-on-write array — content changes ↔ new reference.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    [UpdateBefore(typeof(NearbyAudioBindingSystem))]
    [LogCategory(ReportCategory.NEARBY_VOICE_CHAT)]
    public partial class NearbyLivekitBridgeSystem : BaseUnityLoopSystem
    {
        private readonly INearbyAudioStreamRegistry registry;

        internal NearbyLivekitBridgeSystem(World world, INearbyAudioStreamRegistry registry) : base(world)
        {
            this.registry = registry;
        }

        protected override void Update(float t)
        {
            // Order matters:
            // 1. Attach StreamingAudioComponent to avatars whose stream just appeared.
            // 2. Refresh / cascade-remove StreamingAudioComponent on avatars that already carry it.
            // 3. Tag avatars whose active-speaker signal just rose (only those still streaming).
            // 4. Untag avatars whose active-speaker signal just dropped.
            AddStreamingQuery(World);
            UpdateStreamingQuery(World);
            AddSpeakingQuery(World);
            RemoveSpeakingQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(StreamingAudioComponent))]
        [All(typeof(AvatarBase))]
        private void AddStreaming(Entity entity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (string.IsNullOrEmpty(walletId)) return;

            // Read profile (in-ref) and registry BEFORE the structural change. After World.Add
            // the `in profile` ref is invalidated, but the local `walletId` copy is fine.
            string[]? arr = registry.GetAudioSidsArray(walletId);
            if (arr == null) return;

            World.Add(entity, new StreamingAudioComponent(arr));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(StreamingAudioComponent))]
        private void UpdateStreaming(Entity entity, in Profile profile, ref StreamingAudioComponent streaming)
        {
            // Value-copy the wallet id BEFORE any structural change so the `in profile` ref
            // is fully consumed before World.Remove invalidates it.
            string walletId = profile.UserId;
            string[]? current = string.IsNullOrEmpty(walletId)
                ? null
                : registry.GetAudioSidsArray(walletId);

            if (current == null)
            {
                // Stream disappeared — cascade-remove StreamingAudioComponent and the dependent
                // archetype tags on the same tick. From this point on the `ref streaming` is
                // invalid (structural change relocates the component); we must NOT touch it again.
                World.Remove<StreamingAudioComponent>(entity);

                if (World.Has<IsActivelySpeakingTag>(entity))
                    World.Remove<IsActivelySpeakingTag>(entity);

                if (World.Has<IsSuspendedTag>(entity))
                    World.Remove<IsSuspendedTag>(entity);

                if (World.Has<InAudibleRangeTag>(entity))
                    World.Remove<InAudibleRangeTag>(entity);

                return;
            }

            // Refresh path — ref-mutation only, NO structural change. Cheap when the registry's
            // COW array reference has not changed since the last observation (the dominant case
            // in steady state).
            if (!ReferenceEquals(streaming.SidsSnapshot, current))
                streaming.SidsSnapshot = current;
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(IsActivelySpeakingTag))]
        [All(typeof(AvatarBase), typeof(StreamingAudioComponent))]
        private void AddSpeaking(Entity entity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (!string.IsNullOrEmpty(walletId) && registry.IsActiveSpeaker(walletId))
                World.Add<IsActivelySpeakingTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(IsActivelySpeakingTag))]
        private void RemoveSpeaking(Entity entity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (!string.IsNullOrEmpty(walletId) && !registry.IsActiveSpeaker(walletId))
                World.Remove<IsActivelySpeakingTag>(entity);
        }
    }
}
