using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Profiles;
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

        
        private CancellationTokenSource? fetchProfileCancellationToken;

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
                                 .AddStringFieldWithConfirmation("0x..", "Set Address", UpdateProfileForOwnAvatarAsync);
        }

        protected override void Update(float t)
        {
            widgetVisibility?.SetVisible(realmData.Configured);
        }

        private async void UpdateProfileForOwnAvatarAsync(string profileId)
        {
            const int VERSION = 0;

            var newProfile = await profileRepository.GetAsync(profileId, VERSION, CancellationToken.None);
            World.Set(ownPlayerEntity, newProfile);
        }
    }
}
