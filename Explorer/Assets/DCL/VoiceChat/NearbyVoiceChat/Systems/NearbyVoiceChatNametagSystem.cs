using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms;

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
        private readonly IRoom islandRoom;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly NearbyMuteService muteService;

        internal NearbyVoiceChatNametagSystem(
            World world,
            Entity playerEntity,
            IRoom islandRoom,
            NearbyVoiceChatStateModel stateModel,
            NearbyMuteService muteService) : base(world)
        {
            this.playerEntity = playerEntity;
            this.islandRoom = islandRoom;
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

            UpdateExistingNearbyNametagsQuery(World);
            AddMissingNearbyNametagsQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void FlagNearbyNametagsForRemoval(ref VoiceChatNametagComponent c)
        {
            if (c.Type != VoiceChatType.NEARBY || c.IsRemoving) return;
            c = new VoiceChatNametagComponent(false, VoiceChatType.NEARBY) { IsRemoving = true };
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase))]
        private void UpdateExistingNearbyNametags(Entity entity, in Profile profile, ref VoiceChatNametagComponent c)
        {
            if (c.Type != VoiceChatType.NEARBY) return;

            bool shouldShow = ShouldShow(entity, profile.UserId, out bool isSpeaking, out bool isHushed);

            if (!shouldShow)
            {
                if (!c.IsRemoving)
                    c = new VoiceChatNametagComponent(false, VoiceChatType.NEARBY) { IsRemoving = true };
                return;
            }

            if (c.IsSpeaking != isSpeaking || c.IsHushed != isHushed || c.IsRemoving)
                c = new VoiceChatNametagComponent(isSpeaking, VoiceChatType.NEARBY, isHushed);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(VoiceChatNametagComponent))]
        [All(typeof(AvatarBase))]
        private void AddMissingNearbyNametags(Entity entity, in Profile profile)
        {
            if (!ShouldShow(entity, profile.UserId, out bool isSpeaking, out bool isHushed)) return;

            World.Add(entity, new VoiceChatNametagComponent(isSpeaking, VoiceChatType.NEARBY, isHushed));
        }

        private bool ShouldShow(Entity entity, string walletId, out bool isSpeaking, out bool isHushed)
        {
            isSpeaking = false;
            isHushed = false;

            if (string.IsNullOrEmpty(walletId)) return false;

            bool isLocal = entity == playerEntity;
            if (isLocal)
            {
                if (stateModel.State.Value == NearbyVoiceChatState.OPEN_MIC)
                {
                    isSpeaking = islandRoom.ActiveSpeakers.Contains(walletId);
                    isHushed = false;
                    return true;
                }

                return false;
            }

            if (islandRoom.ActiveSpeakers.Contains(walletId))
            {
                isSpeaking = true;
                isHushed = muteService.IsMuted(walletId);
                return true;
            }

            return false;
        }
    }
}
