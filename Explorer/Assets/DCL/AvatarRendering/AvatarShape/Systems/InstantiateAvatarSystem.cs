using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CreateAvatarSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateAvatarSystem : BaseUnityLoopSystem
    {

        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;
        private SingleInstanceEntity wearableCatalog;


        public InstantiateAvatarSystem(World world, IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider,
            IComponentPool<AvatarBase> avatarPoolRegistry) : base(world)
        {
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.avatarPoolRegistry = avatarPoolRegistry;
        }

        public override void Initialize()
        {
            base.Initialize();
            wearableCatalog = World.CacheWearableCatalog();
        }

        protected override void Update(float t)
        {
            //TODO: Release flow
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

                AvatarBase avatarBase = avatarPoolRegistry.Get();
                avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";
                avatarBase.transform.position = new Vector3(Random.Range(0, 20), 0, Random.Range(0, 20));

                //Instantiation and binding bones of avatar
                GameObject bodyShape = InstantiateWearable(avatarShapeComponent.BodyShapeUrn, World.Get<WearableComponent>(avatarShapeComponent.BodyShape),
                    avatarBase.AvatarSkinnedMeshRenderer, avatarBase.transform);
                HideBodyParts(bodyShape);

                for (var i = 0; i < avatarShapeComponent.Wearables.Length; i++)
                    InstantiateWearable(avatarShapeComponent.BodyShapeUrn,
                        World.Get<WearableComponent>(avatarShapeComponent.Wearables[i]), avatarBase.AvatarSkinnedMeshRenderer, avatarBase.transform);

            }
        }

        //TODO: Do a proper hiding algorithm
        private void HideBodyParts(GameObject bodyShape)
        {
            for (var i = 0; i < bodyShape.transform.childCount; i++)
            {
                bool turnOff = !(bodyShape.transform.GetChild(i).name.Contains("uBody_BaseMesh") ||
                                 bodyShape.transform.GetChild(i).name.Contains("lBody_BaseMesh") ||
                                 bodyShape.transform.GetChild(i).name.Contains("Feet_BaseMesh"));

                bodyShape.transform.GetChild(i).gameObject.SetActive(turnOff);
            }
        }

        private GameObject InstantiateWearable(string bodyShape, WearableComponent wearableComponent, SkinnedMeshRenderer baseAvatar, Transform parentTransform)
        {
            GameObject objectToInstantiate = wearableComponent.AssetBundleData.GameObject;

            //TODO: Instantiating default wearable
            if (objectToInstantiate == null)
            {
                string defaultUrn = WearablesLiterals.DefaultWearables.GetDefaultWearable(bodyShape, wearableComponent.wearableContent.category);
                objectToInstantiate = World.Get<WearableComponent>(wearableCatalog.GetWearableCatalog(World).catalog[defaultUrn]).AssetBundleData.GameObject;
            }

            //TODO: Pooling and combining
            GameObject instantiatedWearabled = Object.Instantiate(objectToInstantiate, parentTransform);

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in instantiatedWearabled.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinnedMeshRenderer.rootBone = baseAvatar.rootBone;
                skinnedMeshRenderer.bones = baseAvatar.bones;
            }

            return instantiatedWearabled;
        }

        private bool IsWearableReadyToInstantiate(ref WearableComponent wearableComponent)
        {
            if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleLoaded) { return true; }

            if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleNotLoaded)
            {
                //TODO: Not completely happy to initialize the download of the asset bundle here.
                //Also, the URL is resolved in the DownloadAssetBundleManifestSystem. Should a prepare system be done?
                wearableComponent.wearableAssetBundleManifestPromise =
                    AssetPromise<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>.Create(World,
                        new GetWearableAssetBundleManifestIntention
                        {
                            CommonArguments = new CommonLoadingArguments(wearableComponent.hash),
                            Hash = wearableComponent.hash,
                        },
                        PartitionComponent.TOP_PRIORITY);

                wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleManifestLoading;
            }
            return false;
        }

    }
}
