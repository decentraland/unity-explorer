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
    ///     Runs every tick in <see cref="AvatarGroup"/>, before <see cref="NearbyAudioBindingSystem"/>.
    ///     Stateless — pass-through under the listening gate (markers reflect LiveKit, not nearby-chat policy; consumers gate on policy).
    ///     <para>
    ///         Maintains the avatar's <see cref="NearbyAudioStreamerComponent.StreamSidsSnapshot"/> as a
    ///         reference to the registry's copy-on-write sid array — <c>ReferenceEquals</c> is the
    ///         freshness signal, no version counter is needed.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(NearbyVoiceChatGroup))]
    [UpdateBefore(typeof(NearbyAudibleRangeSystem))]
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
            AddStreamingQuery(World); // 1. Attach NearbyAudioStreamerComponent to avatars whose stream just appeared.
            RefreshStreamingQuery(World); // 2a. Refresh SidsSnapshot reference on avatars that already carry the component (ref-mutation only, no structural changes).
            RemoveStreamingQuery(World); // 2b. Cascade-remove on avatars whose stream disappeared (structural changes).
            AddSpeakingQuery(World); // 3. Tag avatars whose active-speaker signal just rose (only those still streaming).
            RemoveSpeakingQuery(World); // 4. Untag avatars whose active-speaker signal just dropped.
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(NearbyAudioStreamerComponent))]
        [All(typeof(AvatarBase))]
        private void AddStreaming(Entity entity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (string.IsNullOrEmpty(walletId)) return;

            string[]? arr = registry.GetAudioSidsArray(walletId);
            if (arr != null)
                World.Add(entity, new NearbyAudioStreamerComponent(arr));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void RefreshStreaming(in Profile profile, ref NearbyAudioStreamerComponent nearby)
        {
            string userId = profile.UserId;
            if (string.IsNullOrEmpty(userId)) return;

            string[]? current = registry.GetAudioSidsArray(userId);
            if (current == null) return; // cleanup is RemoveStreaming's responsibility

            // Refresh path — registry published a new array (content changed) since we last observed.
            if (!ReferenceEquals(nearby.StreamSidsSnapshot, current))
                nearby.StreamSidsSnapshot = current;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(NearbyAudioStreamerComponent))]
        private void RemoveStreaming(Entity entity, in Profile profile)
        {
            string userId = profile.UserId;
            if (string.IsNullOrEmpty(userId) || registry.GetAudioSidsArray(userId) != null) return;

            // Drop NearbyAudioStreamerComponent and every dependent marker so invariants (speaking ⊆ streaming, audible ⊆ streaming) hold.
            World.Remove<NearbyAudioStreamerComponent>(entity);

            if (World.Has<IsActivelySpeakingTag>(entity))
                World.Remove<IsActivelySpeakingTag>(entity);

            if (World.Has<InAudibleRangeTag>(entity))
                World.Remove<InAudibleRangeTag>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(IsActivelySpeakingTag))]
        [All(typeof(AvatarBase), typeof(NearbyAudioStreamerComponent))]
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
