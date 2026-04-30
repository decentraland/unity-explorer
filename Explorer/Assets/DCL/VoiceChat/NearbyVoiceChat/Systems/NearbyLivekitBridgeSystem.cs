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
    ///         Maintains the avatar's <see cref="StreamingAudioComponent.SidsSnapshot"/> as a
    ///         reference to the registry's copy-on-write sid array — <c>ReferenceEquals</c> is the
    ///         freshness signal, no version counter is needed.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
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

            AddStreamingQuery(World); // 1. Attach StreamingAudioComponent to avatars whose stream just appeared.
            UpdateStreamingQuery(World); // 2. Refresh / cascade-remove on avatars that already carry the component
            AddSpeakingQuery(World); // 3. Tag avatars whose active-speaker signal just rose (only those still streaming).
            RemoveSpeakingQuery(World); // 4. Untag avatars whose active-speaker signal just dropped.
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(StreamingAudioComponent))]
        [All(typeof(AvatarBase))]
        private void AddStreaming(Entity entity, in Profile profile)
        {
            string walletId = profile.UserId;
            if (string.IsNullOrEmpty(walletId)) return;

            string[]? arr = registry.GetAudioSidsArray(walletId);
            if (arr != null)
                World.Add(entity, new StreamingAudioComponent(arr));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(StreamingAudioComponent))]
        private void UpdateStreaming(Entity entity, in Profile profile, ref StreamingAudioComponent streaming)
        {
            string userId = profile.UserId;
            if (string.IsNullOrEmpty(userId)) return;

            string[]? current = registry.GetAudioSidsArray(userId);

            if (current != null)
            {
                // Refresh path — registry published a new array (content changed) since we last observed. ref-mutation;
                if (!ReferenceEquals(streaming.SidsSnapshot, current))
                    streaming.SidsSnapshot = current;
            }
            else
            {
                // drop StreamingAudioComponent and every dependent marker so invariants (speaking ⊆ streaming, audible ⊆ streaming, suspended ⊆ audible) hold.
                World.Remove<StreamingAudioComponent>(entity);

                if (World.Has<IsActivelySpeakingTag>(entity))
                    World.Remove<IsActivelySpeakingTag>(entity);

                if (World.Has<IsSuspendedTag>(entity))
                    World.Remove<IsSuspendedTag>(entity);

                if (World.Has<InAudibleRangeTag>(entity))
                    World.Remove<InAudibleRangeTag>(entity);
            }
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
