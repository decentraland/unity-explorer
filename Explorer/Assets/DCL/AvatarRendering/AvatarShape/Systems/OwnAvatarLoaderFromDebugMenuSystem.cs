using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Profiles;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System.Threading;
using Promise = ECS.StreamableLoading.Common.AssetPromise<
    DCL.Profiles.Profile,
    DCL.Profiles.GetProfileIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class OwnAvatarLoaderFromDebugMenuSystem : BaseUnityLoopSystem
    {
        private readonly Entity ownPlayerEntity;
        private readonly IRealmData realmData;
        private readonly DebugWidgetVisibilityBinding widgetVisibility;

        private CancellationTokenSource fetchProfileCancellationToken;

        public OwnAvatarLoaderFromDebugMenuSystem(
            World world,
            Entity ownPlayerEntity,
            IDebugContainerBuilder debugContainerBuilder,
            IRealmData realmData)
            : base(world)
        {
            this.ownPlayerEntity = ownPlayerEntity;
            this.realmData = realmData;

            debugContainerBuilder.AddWidget("Profile: Avatar Shape")
                                 .SetVisibilityBinding(widgetVisibility = new DebugWidgetVisibilityBinding(false))
                                 .AddStringFieldWithConfirmation("0x..", "Set Address", UpdateProfileForOwnAvatar);
        }

        protected override void Update(float t)
        {
            widgetVisibility.SetVisible(realmData.Configured);
        }

        private void UpdateProfileForOwnAvatar(string profileId)
        {
            const int VERSION = 0;

            var promise = Promise.Create(World,
                new GetProfileIntention(profileId, VERSION),
                PartitionComponent.TOP_PRIORITY);

            World.Add(ownPlayerEntity, promise);
        }
    }
}
