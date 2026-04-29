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
    ///     markers. Runs every tick in <see cref="AvatarGroup"/>, before
    ///     <see cref="NearbyAudioBindingSystem"/>. Stateless — pass-through under the listening gate
    ///     (markers reflect LiveKit, not nearby-chat policy; consumers gate on policy).
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
            // 1. Tag avatars whose stream just appeared.
            // 2. Untag avatars whose stream just disappeared (cascades speaking removal).
            // 3. Tag avatars whose active-speaker signal just rose (only those still streaming).
            // 4. Untag avatars whose active-speaker signal just dropped.
            AddStreamingTagQuery(World);
            RemoveStreamingTagQuery(World);
            AddSpeakingTagQuery(World);
            RemoveSpeakingTagQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(IsStreamingAudioTag))]
        [All(typeof(AvatarBase))]
        private void AddStreamingTag(Entity entity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (!string.IsNullOrEmpty(walletId) && registry.GetAudioSids(walletId) != null)
                World.Add<IsStreamingAudioTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(IsStreamingAudioTag))]
        private void RemoveStreamingTag(Entity entity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (string.IsNullOrEmpty(walletId) || registry.GetAudioSids(walletId) != null) return;

            World.Remove<IsStreamingAudioTag>(entity);

            // Cascade: maintain invariant I1 (speaking ⊆ streaming) on the same tick.
            // Must stay inside the "stream actually disappeared" branch — otherwise every
            // active speaker pays a structural-change pair per tick (Remove here, Add by
            // Query C next call) for no reason.
            if (World.Has<IsActivelySpeakingTag>(entity))
                World.Remove<IsActivelySpeakingTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(IsActivelySpeakingTag))]
        [All(typeof(AvatarBase), typeof(IsStreamingAudioTag))]
        private void AddSpeakingTag(Entity entity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (!string.IsNullOrEmpty(walletId) && registry.IsActiveSpeaker(walletId))
                World.Add<IsActivelySpeakingTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(IsActivelySpeakingTag))]
        private void RemoveSpeakingTag(Entity entity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (!string.IsNullOrEmpty(walletId) && !registry.IsActiveSpeaker(walletId))
                World.Remove<IsActivelySpeakingTag>(entity);
        }
    }
}
