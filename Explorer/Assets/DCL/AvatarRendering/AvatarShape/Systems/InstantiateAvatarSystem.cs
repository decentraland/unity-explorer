using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LoadAvatarWearableSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateAvatarSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity wearableCatalog;

        //TODO: Integrate the instantiation budget provider
        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;

        public InstantiateAvatarSystem(World world, IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider) : base(world)
        {
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
        }

        public override void Initialize()
        {
            base.Initialize();
            wearableCatalog = World.CacheWearableCatalog();
        }

        protected override void Update(float t)
        {
            //TODO: Avatar query and pooling
            //TODO: Cancel flow
            InstantiateAvatarQuery(World);
        }

        [Query]
        private void InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent)
        {
            if (!instantiationFrameTimeBudgetProvider.TrySpendBudget())
                return;

            if (avatarShapeComponent.Status == AvatarShapeComponent.LifeCycle.LoadingAssetBundles)
            {

                if (!IsWearableReadyToInstantiate(ref World.Get<WearableComponent>(avatarShapeComponent.BodyShape)))
                    return;

                foreach (EntityReference avatarShapeWearable in avatarShapeComponent.Wearables)
                    if (!IsWearableReadyToInstantiate(ref World.Get<WearableComponent>(avatarShapeWearable)))
                        return;

                avatarShapeComponent.Status = AvatarShapeComponent.LifeCycle.LoadingFinished;

                //Instantiation and binding bones of avatar
                SkinnedMeshRenderer baseAvatar =
                    Object.Instantiate(Resources.Load<GameObject>("AvatarBase")).GetComponentInChildren<SkinnedMeshRenderer>();

                InstantiateWearable(World.Get<AssetBundleData>(avatarShapeComponent.BodyShape).GameObject, baseAvatar);
                for (var i = 0; i < avatarShapeComponent.Wearables.Length; i++)
                    InstantiateWearable(World.Get<AssetBundleData>(avatarShapeComponent.Wearables[i]).GameObject, baseAvatar);

            }
        }

        private void InstantiateWearable(GameObject objectToInstantiate, SkinnedMeshRenderer baseAvatar)
        {
            GameObject instantiatedWearabled = Object.Instantiate(objectToInstantiate);

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in instantiatedWearabled.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinnedMeshRenderer.rootBone = baseAvatar.rootBone;
                skinnedMeshRenderer.bones = baseAvatar.bones;
            }
        }

        private bool IsWearableReadyToInstantiate(ref WearableComponent wearableComponent)
        {
            //TODO: We do it this way to load ONE wearable asset bundle at a time
            if (wearableComponent.AssetBundleStatus != WearableComponent.AssetBundleLifeCycle.AssetBundleLoaded)
            {
                if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleNotLoaded)
                {
                    //TODO: Not completely happy to initialize the download of the asset bundle here
                    wearableComponent.wearableAssetBundleManifestPromise =
                        AssetPromise<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>.Create(World,
                            new GetWearableAssetBundleManifestIntention()
                            {
                                CommonArguments = new CommonLoadingArguments("DummyURL"),
                                Hash = wearableComponent.hash,
                            },
                            PartitionComponent.TOP_PRIORITY);
                    wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleManifestLoading;
                }
                return false;
            }
            return true;
        }

    }
}
