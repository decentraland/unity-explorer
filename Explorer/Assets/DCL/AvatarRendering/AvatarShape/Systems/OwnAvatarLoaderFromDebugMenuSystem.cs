using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using ECS;
using ECS.Abstract;
using System.Threading;

namespace DCL.AvatarRendering.AvatarShape
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class OwnAvatarLoaderFromDebugMenuSystem : BaseUnityLoopSystem
    {
        private readonly Entity ownPlayerEntity;
        private readonly IRealmData realmData;
        private readonly DebugWidgetVisibilityBinding? widgetVisibility;
        private readonly IProfileRepository profileRepository;

        public OwnAvatarLoaderFromDebugMenuSystem(
            World world,
            Entity ownPlayerEntity,
            IDebugContainerBuilder debugContainerBuilder,
            IRealmData realmData,
            IProfileRepository profileRepository)
            : base(world)
        {
            this.ownPlayerEntity = ownPlayerEntity;
            this.realmData = realmData;
            this.profileRepository = profileRepository;

            debugContainerBuilder.TryAddWidget("Profile: Avatar Shape")
                                 ?.SetVisibilityBinding(widgetVisibility = new DebugWidgetVisibilityBinding(false))
                                 .AddStringFieldWithConfirmation("0x..", "Set Address", profileId => UpdateProfileForOwnAvatarAsync(profileId).Forget(static e => ReportHub.LogException(e, ReportCategory.AVATAR)));
        }

        protected override void Update(float t)
        {
            widgetVisibility?.SetVisible(realmData.Configured);
        }

        private async UniTask UpdateProfileForOwnAvatarAsync(string profileId)
        {
            const int VERSION = 0;

            Profile? fetched = await profileRepository.GetAsync(profileId, VERSION, CancellationToken.None);

            if (fetched != null)
                ProfileUtils.ReplaceOnEntity(World, ownPlayerEntity, fetched);
        }
    }
}
