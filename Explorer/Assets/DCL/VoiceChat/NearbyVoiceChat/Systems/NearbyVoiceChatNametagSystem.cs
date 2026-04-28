using Arch.Core;
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
            // Queries added in subsequent tasks.
        }
    }
}
