using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LoadWearableSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadDefaultWearablesSystem : BaseUnityLoopSystem
    {
        //TODO: How can I wait for this system to end before starting another one?
        public LoadDefaultWearablesSystem(World world) : base(world) { }

        public override void Initialize()
        {
            base.Initialize();

            //TODO: Fix boxing allocation
            var promise = AssetPromise<WearableDTO[], GetWearableByPointersIntention>.Create(World,
                new GetWearableByPointersIntention
                {
                    //TODO: Should a prepare system be done for the catalyst url?
                    CommonArguments = new CommonLoadingArguments("https://peer.decentraland.org/content/entities/active/"),
                    Pointers = WearablesLiterals.DefaultWearables.GetDefaultWearables(),
                }, PartitionComponent.TOP_PRIORITY);

            var wearablePromiseContainerComponent = new WearablePromiseContainerComponent();
            wearablePromiseContainerComponent.WearableByPointerRequestPromise = promise;
            World.Add(promise.Entity, wearablePromiseContainerComponent);
        }

        protected override void Update(float t)
        {
            CompleteDefaultWearablePromiseQuery(World);
        }

        [Query]
        public void CompleteDefaultWearablePromise(ref WearablePromiseContainerComponent promiseContainerComponent)
        {
            if (promiseContainerComponent.WearableByPointerRequestPromise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> result))
            {
                if (!result.Succeeded)
                    ReportHub.LogError(GetReportCategory(), "Default wearables could not be fetched");
                else
                    GenerateAssetBundleRequest(result.Asset);
            }
        }

        private void GenerateAssetBundleRequest(WearableDTO[] result)
        {
            foreach (WearableDTO wearableDto in result)
            {
                //TODO: Update the Wearable status so the AssetBundle is assigned when the Manifest completes
                AssetPromise<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>.Create(World,
                    new GetWearableAssetBundleManifestIntention
                    {
                        CommonArguments = new CommonLoadingArguments(wearableDto.id),
                        Hash = wearableDto.id,
                    },
                    PartitionComponent.TOP_PRIORITY);
            }
        }
    }
}
