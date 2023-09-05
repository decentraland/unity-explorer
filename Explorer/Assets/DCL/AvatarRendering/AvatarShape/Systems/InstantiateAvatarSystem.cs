using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareAvatarSystem))]
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
        [None(typeof(GetAvatarWearableComponent))]
        private void InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent)
        {
            if (!avatarShapeComponent.IsDirty)
                return;

            if (!instantiationFrameTimeBudgetProvider.TrySpendBudget())
                return;

            if (!IsWearableReadyToInstantiate(avatarShapeComponent.BodyShapeUrn))
                return;

            foreach (string avatarShapeWearable in avatarShapeComponent.WearablesUrn)
                if (!IsWearableReadyToInstantiate(avatarShapeWearable))
                    return;

            AvatarBase avatarBase = avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";
            avatarBase.transform.position = new Vector3(Random.Range(0, 20), 0, Random.Range(0, 20));

            //Instantiation and binding bones of avatar
            GameObject bodyShape = InstantiateWearable(avatarShapeComponent.BodyShapeUrn, avatarBase.AvatarSkinnedMeshRenderer, avatarBase.transform);
            HideBodyParts(bodyShape);

            for (var i = 0; i < avatarShapeComponent.WearablesUrn.Length; i++)
                InstantiateWearable(avatarShapeComponent.WearablesUrn[i], avatarBase.AvatarSkinnedMeshRenderer, avatarBase.transform);

            avatarShapeComponent.IsDirty = false;
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

        private GameObject InstantiateWearable(string wearableUrn, SkinnedMeshRenderer baseAvatar, Transform parentTransform)
        {
            WearableComponent wearableComponent = World.Get<WearableComponent>(wearableCatalog.GetWearableCatalog(World).catalog[wearableUrn]);

            GameObject objectToInstantiate = wearableComponent.AssetBundleData.GameObject;

            //TODO: Pooling and combining
            GameObject instantiatedWearabled = Object.Instantiate(objectToInstantiate, parentTransform);

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in instantiatedWearabled.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinnedMeshRenderer.rootBone = baseAvatar.rootBone;
                skinnedMeshRenderer.bones = baseAvatar.bones;
            }

            return instantiatedWearabled;
        }

        private bool IsWearableReadyToInstantiate(string wearableComponentUrn)
        {
            ref WearableComponent wearableComponent
                = ref World.Get<WearableComponent>(wearableCatalog.GetWearableCatalog(World).catalog[wearableComponentUrn]);

            if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleLoaded)
                return true;

            if (wearableComponent.AssetBundleStatus == WearableComponent.AssetBundleLifeCycle.AssetBundleNotLoaded)
                wearableComponent.AssetBundleStatus = WearableComponent.AssetBundleLifeCycle.AssetBundleRequested;

            return false;
        }

    }
}
