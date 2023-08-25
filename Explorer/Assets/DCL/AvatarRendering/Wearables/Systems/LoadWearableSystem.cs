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
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoadWearableSystem : BaseUnityLoopSystem
    {
        private readonly string WEARABLE_DEFINITION_URL;
        private readonly string WEARABLE_CONTENT_BASE_URL;
        private string WEARABLE_CONTENT_ASSET_BUNDLE_URL;

        public LoadWearableSystem(World world) : base(world)
        {
            WEARABLE_DEFINITION_URL = "https://peer.decentraland.org/content/entities/active";
            WEARABLE_CONTENT_BASE_URL = "";
        }

        protected override void Update(float t)
        {
            //TODO: Im request Definition, Manifest and AB for every wearable. How can we avoid this?
            StartWearableLoadQuery(World);
            ParseWearableDTOQuery(World);
            DownloadWearableABQuery(World);
        }

        [Query]
        private void StartWearableLoad(ref WearableComponent wearableComponent)
        {
            if (wearableComponent.Status == WearableComponent.LifeCycle.LoadingNotStarted)
            {
                wearableComponent.wearableDTOPromise =
                    AssetPromise<WearableDTO, GetWearableIntention>.Create(World,
                        new GetWearableIntention
                        {
                            CommonArguments = new CommonLoadingArguments(WEARABLE_DEFINITION_URL),
                            Pointer = wearableComponent.urn,
                        }, PartitionComponent.TOP_PRIORITY);

                wearableComponent.Status = WearableComponent.LifeCycle.LoadingDefinition;
            }
        }

        [Query]
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
        }

        [Query]
        private void DownloadWearableAB(ref WearableComponent wearableComponent)
        {
            if (wearableComponent.Status == WearableComponent.LifeCycle.LoadingAssetBundle)

                //TODO: Handle failures request
                if (wearableComponent.wearableAssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> assetBundleData))
                {
                    Object.Instantiate(assetBundleData.Asset.GameObject);
                    wearableComponent.Status = WearableComponent.LifeCycle.LoadingFinished;
                }
        }
    }
}
