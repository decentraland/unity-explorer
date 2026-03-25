using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.ECSComponents;
using DCL.Profiles;
using ECS.Abstract;

namespace DCL.Analytics.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarLoaderSystem))]
    [UpdateBefore(typeof(AvatarInstantiatorSystem))]
    public partial class AvatarLoaderAnalyticsSystem : BaseUnityLoopSystem
    {
        internal AvatarLoaderAnalyticsSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ReInitializeAnalyticsFromSDKComponentQuery(World);
            ReInitializeAnalyticsFromProfileQuery(World);

            InitializeAnalyticsQuery(World);
        }

        [Query]
        [None(typeof(AvatarAnalytics), typeof(AvatarCustomSkinningComponent), typeof(AvatarBase), typeof(AvatarTransformMatrixComponent))]
        private void InitializeAnalytics(Entity entity, in AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.WearableLoading.Status == AvatarShapeComponent.WearableLoadingStatus.None)
                return;

            World.Add(entity, new AvatarAnalytics(
                UnityEngine.Time.realtimeSinceStartup,
                avatarShapeComponent.WearableLoading.LoadingIntention.Pointers.Count));
        }

        [Query]
        private void ReInitializeAnalyticsFromProfile(Profile profile, in AvatarShapeComponent avatarShapeComponent, ref AvatarAnalytics avatarAnalytics)
        {
            if (profile.IsDirty && avatarShapeComponent.WearableLoading.Status != AvatarShapeComponent.WearableLoadingStatus.None)
            {
                avatarAnalytics = new AvatarAnalytics(
                    UnityEngine.Time.realtimeSinceStartup,
                    avatarShapeComponent.WearableLoading.LoadingIntention.Pointers.Count);
            }
        }

        [Query]
        private void ReInitializeAnalyticsFromSDKComponent(PBAvatarShape avatarShape, in AvatarShapeComponent avatarShapeComponent, ref AvatarAnalytics avatarAnalytics)
        {
            if (avatarShape.IsDirty && avatarShapeComponent.WearableLoading.Status != AvatarShapeComponent.WearableLoadingStatus.None)
            {
                avatarAnalytics = new AvatarAnalytics(
                    UnityEngine.Time.realtimeSinceStartup,
                    avatarShapeComponent.WearableLoading.LoadingIntention.Pointers.Count);
            }
        }
    }
}
