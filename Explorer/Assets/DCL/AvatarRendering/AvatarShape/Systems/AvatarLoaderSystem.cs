using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.Unity.ColorComponent;
using Promise = ECS.StreamableLoading.Common.AssetPromise<
    DCL.AvatarRendering.Wearables.Components.IWearable[],
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarLoaderSystem : BaseUnityLoopSystem
    {
        internal AvatarLoaderSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            LoadNewAvatarQuery(World);
            UpdateAvatarQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void LoadNewAvatar(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            Promise wearablePromise = CreateWearablePromise(pbAvatarShape, partition);
            pbAvatarShape.IsDirty = false;
            World.Add(entity, new AvatarShapeComponent(pbAvatarShape.Name, pbAvatarShape.Id, pbAvatarShape, wearablePromise, pbAvatarShape.SkinColor.ToUnityColor(), pbAvatarShape.HairColor.ToUnityColor()));
        }

        [Query]
        private void UpdateAvatar(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            Promise newPromise = CreateWearablePromise(pbAvatarShape, partition);
            avatarShapeComponent.WearablePromise = newPromise;

            avatarShapeComponent.BodyShape = pbAvatarShape;
            avatarShapeComponent.IsDirty = true;
            pbAvatarShape.IsDirty = false;
        }

        private Promise CreateWearablePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition) =>
            Promise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(pbAvatarShape, pbAvatarShape.Wearables),
                partition);
    }
}
