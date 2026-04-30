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
    ///     Pull-mirror from <see cref="INearbyAudioStreamRegistry"/> to avatar-entity components.
    ///     Runs every tick in <see cref="AvatarGroup"/>, before
    ///     <see cref="NearbyAudioBindingSystem"/>. Stateless — pass-through under the listening gate
    ///     (markers reflect LiveKit, not nearby-chat policy; consumers gate on policy).
    ///     <para>
    ///         Maintains the avatar's <see cref="StreamingAudioComponent.SidsSnapshot"/> as a
    ///         reference to the registry's copy-on-write sid array — <c>ReferenceEquals</c> is the
    ///         freshness signal, no version counter is needed.
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
            // 2. Refresh / cascade-remove on avatars that already carry the component (drives the
            //    speaking / suspended / range-tag cascade when the stream disappears).
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
            // Read profile (in-param) FIRST, then registry, then perform the structural change.
            // CLAUDE.md hard rule: no World.Add/Remove while any in/ref/out reference is outstanding.
            string walletId = profile.UserId;
            if (string.IsNullOrEmpty(walletId)) return;

            string[]? arr = registry.GetAudioSidsArray(walletId);
            if (arr == null) return;

            World.Add(entity, new StreamingAudioComponent(arr));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(StreamingAudioComponent))]
        private void UpdateStreaming(Entity entity, in Profile profile, ref StreamingAudioComponent streaming)
        {
            // Value-copy walletId out of the in-param BEFORE the registry call, so no in/ref reads
            // cross the structural-change boundary in the cascade branch.
            string userId = profile.UserId;
            if (string.IsNullOrEmpty(userId)) return;

            string[]? current = registry.GetAudioSidsArray(userId);

            if (current == null)
            {
                // Cascade — the same shape as the previous RemoveStreamingTag path:
                // drop StreamingAudioComponent and every dependent marker so invariants
                // (speaking ⊆ streaming, audible ⊆ streaming, suspended ⊆ audible) hold.
                // The `ref streaming` and `in profile` references are not touched after this point.
                World.Remove<StreamingAudioComponent>(entity);

                if (World.Has<IsActivelySpeakingTag>(entity))
                    World.Remove<IsActivelySpeakingTag>(entity);

                if (World.Has<IsSuspendedTag>(entity))
                    World.Remove<IsSuspendedTag>(entity);

                if (World.Has<InAudibleRangeTag>(entity))
                    World.Remove<InAudibleRangeTag>(entity);

                return;
            }

            // Refresh path — registry published a new array (content changed) since we last
            // observed. ref-mutation; not a structural change, refs stay valid.
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
