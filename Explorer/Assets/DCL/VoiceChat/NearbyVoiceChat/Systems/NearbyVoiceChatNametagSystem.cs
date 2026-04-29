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
    ///     Owns the <see cref="VoiceChatNametagComponent"/> lifecycle for nearby voice chat through pull-based
    ///     per-frame queries on the avatar set. Replaces <c>NearbyVoiceChatNametagsHandler</c>'s event-driven
    ///     mutation. Three queries: listening-gate bulk teardown, <c>ref</c>-mutation of existing components,
    ///     <c>World.Add</c> of missing components on avatars whose predicate flips to "should show".
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    [LogCategory(ReportCategory.NEARBY_VOICE_CHAT)]
    public partial class NearbyVoiceChatNametagSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly INearbyAudioStreamRegistry registry;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly NearbyMuteService muteService;

        private bool localMicOpen;

        internal NearbyVoiceChatNametagSystem(
            World world,
            Entity playerEntity,
            INearbyAudioStreamRegistry registry,
            NearbyVoiceChatStateModel stateModel,
            NearbyMuteService muteService) : base(world)
        {
            this.playerEntity = playerEntity;
            this.registry = registry;
            this.stateModel = stateModel;
            this.muteService = muteService;
        }

        protected override void Update(float t)
        {
            if (stateModel.IsListeningDisabled)
            {
                FlagNearbyNametagsForRemovalQuery(World);
                return;
            }

            localMicOpen = stateModel.State.Value == NearbyVoiceChatState.OPEN_MIC;

            UpdateExistingNearbyNametagsQuery(World);
            AddMissingNearbyNametagsQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void FlagNearbyNametagsForRemoval(ref VoiceChatNametagComponent c)
        {
            if (c is { Type: VoiceChatType.NEARBY, IsRemoving: false })
                c = new VoiceChatNametagComponent(false, VoiceChatType.NEARBY) { IsRemoving = true };
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void UpdateExistingNearbyNametags(Entity entity, in Profile profile, ref VoiceChatNametagComponent badgeComponent)
        {
            if (badgeComponent.Type != VoiceChatType.NEARBY) return;

            VoiceChatNametagComponent next = Resolve(entity, profile.UserId)
                                             ?? new VoiceChatNametagComponent(false, VoiceChatType.NEARBY) { IsRemoving = true };

            if (badgeComponent.IsSpeaking != next.IsSpeaking || badgeComponent.IsHushed != next.IsHushed || badgeComponent.IsRemoving != next.IsRemoving)
                badgeComponent = next;
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(VoiceChatNametagComponent))]
        [All(typeof(AvatarBase))]
        private void AddMissingNearbyNametags(Entity entity, in Profile profile)
        {
            VoiceChatNametagComponent? resolved = Resolve(entity, profile.UserId);

            if (resolved != null)
                World.Add(entity, resolved.Value);
        }

        private VoiceChatNametagComponent? Resolve(Entity entity, string walletId)
        {
            if (string.IsNullOrEmpty(walletId)) return null;

            return entity == playerEntity
                ? ResolveLocal(walletId)
                : ResolveRemote(walletId);
        }

        private VoiceChatNametagComponent? ResolveLocal(string walletId)
        {
            if (!localMicOpen) return null;

            bool isSpeaking = registry.IsActiveSpeaker(walletId);
            return new VoiceChatNametagComponent(isSpeaking, VoiceChatType.NEARBY);
        }

        private VoiceChatNametagComponent? ResolveRemote(string walletId)
        {
            if (!registry.IsActiveSpeaker(walletId)) return null;

            bool isHushed = muteService.IsMuted(walletId);
            return new VoiceChatNametagComponent(isSpeaking: true, VoiceChatType.NEARBY, isHushed);
        }
    }
}
