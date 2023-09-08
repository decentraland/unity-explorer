using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Components;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareAvatarSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateAvatarSystem : BaseUnityLoopSystem
    {
        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;

        public InstantiateAvatarSystem(World world, IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider,
            IComponentPool<AvatarBase> avatarPoolRegistry) : base(world)
        {
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.avatarPoolRegistry = avatarPoolRegistry;
        }

        protected override void Update(float t)
        {
            InstantiateAvatarQuery(World);
        }


        [Query]
        public void InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent, ref TransformComponent transformComponent)
        {
            if (!avatarShapeComponent.IsDirty)
                return;

            if (!instantiationFrameTimeBudgetProvider.TrySpendBudget())
                return;

            if (!avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableLoadingResult<Wearable[]> wearablesResult)) return;

            AvatarBase avatarBase = avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";

            Transform avatarTransform = avatarBase.transform;

            avatarTransform.SetParent(transformComponent.Transform, false);
            avatarTransform.ResetLocalTRS();

            foreach (Wearable wearable in wearablesResult.Asset)
            {
                GameObject instantiateWearable = InstantiateWearable(wearable.AssetBundleData[avatarShapeComponent.BodyShape].Value.Asset.GameObject, avatarBase.AvatarSkinnedMeshRenderer, avatarTransform);

                //TODO: Do a proper hiding algorithm
                if (wearable.IsBodyShape())
                    HideBodyParts(instantiateWearable);
            }

            avatarShapeComponent.IsDirty = false;
        }

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

        private GameObject InstantiateWearable(GameObject wearableToInstantiate, SkinnedMeshRenderer baseAvatar, Transform parentTransform)
        {
            //TODO: Pooling and combining
            GameObject instantiatedWearable = Object.Instantiate(wearableToInstantiate, parentTransform);
            instantiatedWearable.transform.ResetLocalTRS();

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in instantiatedWearable.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinnedMeshRenderer.rootBone = baseAvatar.rootBone;
                skinnedMeshRenderer.bones = baseAvatar.bones;
            }

            return instantiatedWearable;
        }
    }
}
