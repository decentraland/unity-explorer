using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LoadWearableSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class LoadAvatarWearableSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity wearableCatalog;

        public LoadAvatarWearableSystem(World world) : base(world) { }

        public override void Initialize()
        {
            base.Initialize();
            wearableCatalog = World.CacheWearableCatalog();
        }

        protected override void Update(float t)
        {
            StartAvatarWearableLoadQuery(World);
            FinalizeAvatarWearableLoadQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void StartAvatarWearableLoad(in Entity entity, ref PBAvatarShape pbAvatarShape)
        {
            var avatarShapeComponent = new AvatarShapeComponent
            {
                ID = pbAvatarShape.Id,
                BodyShape = EntityReference.Null,
                Wearables = new EntityReference[pbAvatarShape.Wearables.Count],
            };
            List<string> missingWearables;

            if (AreWearablesReady(ref pbAvatarShape, out missingWearables))
                SetAvatarWearables(ref pbAvatarShape, ref avatarShapeComponent);
            else
            {
                avatarShapeComponent.WearablePromise = AssetPromise<WearableDTO[], GetWearableByPointersIntention>.Create(World,
                    new GetWearableByPointersIntention
                    {
                        //TODO: Should a prepare system be done for the catalyst url?
                        CommonArguments = new CommonLoadingArguments("https://peer.decentraland.org/content/entities/active/"),
                        Pointers = missingWearables.ToArray(),
                    }, PartitionComponent.TOP_PRIORITY);
            }

            World.Add(entity, avatarShapeComponent);
        }

        [Query]
        private void FinalizeAvatarWearableLoad(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.Status == AvatarShapeComponent.LifeCycle.LoadingWearables &&
                avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> result))
            {
                if (!result.Succeeded)
                    ReportHub.LogError(GetReportCategory(), "Error loading wearables for avatar: " + avatarShapeComponent.ID);
                else
                    SetAvatarWearables(ref pbAvatarShape, ref avatarShapeComponent);
            }
        }

        private void SetAvatarWearables(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShape)
        {
            avatarShape.BodyShape = GetEntityReference(pbAvatarShape.BodyShape);
            for (var i = 0; i < pbAvatarShape.Wearables.Count; i++)
                avatarShape.Wearables[i] = GetEntityReference(pbAvatarShape.Wearables[i]);

            avatarShape.Status = AvatarShapeComponent.LifeCycle.LoadingAssetBundles;
        }

        private bool AreWearablesReady(ref PBAvatarShape pbAvatarShape, out List<string> missingWearables)
        {
            missingWearables = new List<string>();

            if (!IsWearableInCatalog(pbAvatarShape.BodyShape))
                missingWearables.Add(pbAvatarShape.BodyShape);

            for (var i = 0; i < pbAvatarShape.Wearables.Count; i++)
                if (!IsWearableInCatalog(pbAvatarShape.BodyShape))
                    missingWearables.Add(pbAvatarShape.Wearables[i]);

            return missingWearables.Count == 0;
        }

        private bool IsWearableInCatalog(string urnToAnalyze) =>
            wearableCatalog.GetWearableCatalog(World).catalog.ContainsKey(urnToAnalyze);

        private EntityReference GetEntityReference(string urnToAnalyze) =>
            wearableCatalog.GetWearableCatalog(World).catalog[urnToAnalyze];

    }
}
