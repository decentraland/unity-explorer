using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadWearableSystem : BaseUnityLoopSystem
    {
        private readonly string WEARABLE_DEFINITION_URL;
        private readonly string WEARABLE_CONTENT_BASE_URL;
        private string WEARABLE_CONTENT_ASSET_BUNDLE_URL;

        private SingleInstanceEntity wearableCatalog;

        //TODO: Integrate the instantiation budget provider
        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;

        public LoadWearableSystem(World world) : base(world)
        {
            WEARABLE_DEFINITION_URL = "https://peer.decentraland.org/content/entities/active";
            WEARABLE_CONTENT_BASE_URL = "";
        }

        public override void Initialize()
        {
            base.Initialize();
            wearableCatalog = World.CacheWearableCatalog();

            //Request base wearables
            /*AssetPromise<BaseWearablesListResponse, GetBaseWearableIntention>.Create(World,
                new GetBaseWearableIntention()
                {
                    CommonArguments = new CommonLoadingArguments("https://peer.decentraland.org/content/entities/active/collections/urn:decentraland:off-chain:base-avatars"),
                }, PartitionComponent.TOP_PRIORITY);*/
        }

        protected override void Update(float t)
        {
            //TODO: Im request Definition, Manifest and AB for every wearable. How can we avoid this?
            CreateWearableByParamRequestQuery(World);

            //CreateWearableByPointerRequestQuery(World);

            CreateWearableComponentFromResultQuery(World);

            //StartWearableLoadQuery(World);
            //ParseWearableDTOQuery(World);
            //DownloadWearableABQuery(World);
        }

        [Query]
        private void CreateWearableByParamRequest(ref GetWearableByParamIntention wearableByParamIntention)
        {
            AssetPromise<WearableDTO, GetWearableByParamIntention>.Create(World,
                wearableByParamIntention, PartitionComponent.TOP_PRIORITY);
        }

        /*[Query]
        private void StartWearableLoad(in Entity entity, ref GetWearableByPointersIntention wearableComponent)
        {
            if (wearableComponent.Status == WearableComponent.LifeCycle.LoadingNotStarted)
            {
                    AssetPromise<WearableDTO, GetWearableByPointersIntention>.Create(World,
                        new GetWearableByPointersIntention
                        {
                            CommonArguments = new CommonLoadingArguments(WEARABLE_DEFINITION_URL),
                            Pointers = new List<string>(),
                        }, PartitionComponent.TOP_PRIORITY);
                wearableComponent.Status = WearableComponent.LifeCycle.LoadingDefinition;
                wearableCatalog.GetWearableCatalog(World).catalog.Add(wearableComponent.urn, World.Reference(entity));
            }
        }*/


        [Query]
        private void CreateWearableComponentFromResult(in Entity entity, ref StreamableLoadingResult<WearableDTO[]> wearableDTOResult)
        {
            // If the result failed, the result will be handled and entity destroyed by the system that requested the wearables
            if (!wearableDTOResult.Succeeded)
                return;

            foreach (WearableDTO assetEntity in wearableDTOResult.Asset)
            {
                WearableComponent wearableComponent = assetEntity.ToWearableItem(WEARABLE_CONTENT_BASE_URL);
                Entity wearableEntity = World.Create(wearableComponent);
                wearableCatalog.GetWearableCatalog(World).catalog.Add(wearableComponent.urn, World.Reference(wearableEntity));
            }
        }

        /*[Query]
        private void ParseWearableDTO(ref WearableComponent wearableComponent)
        {
            if (wearableComponent.Status == WearableComponent.LifeCycle.LoadingDefinition)

                //TODO: Handle failures request
                //TODO: Download thumbnail
                if (wearableComponent.wearableDTOPromise.TryConsume(World, out StreamableLoadingResult<WearableDTO> wearableDTOResult))
                {
                    wearableDTOResult.Asset.ToWearableItem(ref wearableComponent, WEARABLE_CONTENT_BASE_URL);

                    if (!wearableComponent.AssetBundleManifest.Equals(SceneAssetBundleManifest.NULL))
                    {
                        wearableComponent.wearableAssetBundlePromise =
                            AssetPromise<AssetBundleData, GetWearableAssetBundleIntention>.Create(World,
                                GetWearableAssetBundleIntention.FromHash(wearableComponent.AssetBundleManifest, wearableComponent.GetMainFileHash() + PlatformUtils.GetPlatform())
                              , PartitionComponent.TOP_PRIORITY);

                        wearableComponent.Status = WearableComponent.LifeCycle.LoadingAssetBundle;
                    }
                    else
                    {
                        Debug.Log(wearableComponent.urn + " has no AssetBundleManifest");
                        wearableComponent.Status = WearableComponent.LifeCycle.LoadingFinished;
                    }
                }
        }*/

        [Query]
        private void DownloadWearableAB(in Entity entity, ref WearableComponent wearableComponent)
        {
            if (wearableComponent.Status == WearableComponent.LifeCycle.LoadingAssetBundle)

                //TODO: Handle failures request
                if (wearableComponent.wearableAssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> assetBundleData))
                {
                    wearableComponent.Status = WearableComponent.LifeCycle.LoadingFinished;
                    World.Add(entity, assetBundleData.Asset);
                }
        }
    }
}
