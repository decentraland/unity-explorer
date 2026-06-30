using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
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
    ///     Runs every tick in <see cref="NearbyVoiceChatGroup"/>, before <see cref="NearbyAudioBindingSystem"/>.
    ///     Stateless — pass-through under the listening gate (markers reflect LiveKit, not nearby-chat policy; consumers gate on policy).
    ///     <para>
    ///         Maintains the avatar's <see cref="NearbyAudioStreamerComponent.CurrentSid"/> as the resolver's pick.
    ///         A flip from one active sid to another mutates the field in place; the cleanup system reaps the
    ///         old <c>(walletId, oldSid)</c> audio entity on the next tick and binding creates the new one.
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
            AddStreamingQuery(World); // 1. Attach NearbyAudioStreamerComponent to avatars whose resolver just picked an active sid.
            RefreshStreamingQuery(World); // 2a. Mutate CurrentSid in place when the resolver flipped to a different sid (ref-mutation only, no structural changes).
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

            string? activeSid = registry.GetActiveSid(walletId);
            if (activeSid != null)
                World.Add(entity, new NearbyAudioStreamerComponent(activeSid));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void RefreshStreaming(in Profile profile, ref NearbyAudioStreamerComponent nearby)
        {
            string userId = profile.UserId;
            if (string.IsNullOrEmpty(userId)) return;

            // Null means: registry has no sids (RemoveStreaming handles), or all-zeros window (wait).
            string? activeSid = registry.GetActiveSid(userId);
            if (activeSid == null) return;

            // Cleanup reaps the old (walletId, oldSid) entity.
            if (!string.Equals(nearby.CurrentSid, activeSid, System.StringComparison.Ordinal))
                nearby.CurrentSid = activeSid;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(NearbyAudioStreamerComponent))]
        private void RemoveStreaming(Entity entity, in Profile profile)
        {
            string userId = profile.UserId;
            if (string.IsNullOrEmpty(userId) || registry.HasAudioStream(userId)) return;

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
