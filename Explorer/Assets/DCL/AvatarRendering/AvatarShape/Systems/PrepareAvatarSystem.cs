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
using System.Linq;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareWearableSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class PrepareAvatarSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity wearableCatalog;
        private readonly string CATALYST_URL;

        public PrepareAvatarSystem(World world, string catalystURL) : base(world)
        {
            CATALYST_URL = catalystURL;
        }

        public override void Initialize()
        {
            base.Initialize();
            wearableCatalog = World.CacheWearableCatalog();
        }

        protected override void Update(float t)
        {
            StartAvatarLoadQuery(World);
            FinalizeAvatarLoadQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void StartAvatarLoad(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            var avatarShapeComponent = new AvatarShapeComponent
            {
                ID = pbAvatarShape.Id,
                BodyShapeUrn = WearablesLiterals.BodyShapes.DEFAULT,
                WearablesUrn = WearablesLiterals.DefaultWearables.GetDefaultWearablesForBodyShape(WearablesLiterals.BodyShapes.DEFAULT),
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
                        CommonArguments = new CommonLoadingArguments(CATALYST_URL),
                        Pointers = missingWearables.ToArray(),
                        StartAssetBundlesDownload = true,
                    }, partition);
            }
            World.Add(entity, avatarShapeComponent);
        }

        [Query]
        private void FinalizeAvatarLoad(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.Status == AvatarShapeComponent.LifeCycle.LoadingWearables &&
                avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> result))
            {
                if (result.Succeeded)
                    SetAvatarWearables(ref pbAvatarShape, ref avatarShapeComponent);
                else
                    ReportHub.LogError(GetReportCategory(), $"Error loading wearables for avatar: {avatarShapeComponent.ID}. Default wearables will be loaded");

                avatarShapeComponent.Status = AvatarShapeComponent.LifeCycle.LoadingAssetBundles;
            }
        }

        private void SetAvatarWearables(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShape)
        {
            avatarShape.BodyShapeUrn = pbAvatarShape.BodyShape;
            avatarShape.WearablesUrn = pbAvatarShape.Wearables.ToArray();

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
