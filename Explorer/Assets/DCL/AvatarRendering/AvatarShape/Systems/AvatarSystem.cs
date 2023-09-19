using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.Transforms.Components;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarSystem : BaseUnityLoopSystem
    {
        private readonly IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider;
        private readonly IComponentPool<AvatarBase> avatarPoolRegistry;

        public AvatarSystem(World world, IConcurrentBudgetProvider instantiationFrameTimeBudgetProvider,
            IComponentPool<AvatarBase> avatarPoolRegistry) : base(world)
        {
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.avatarPoolRegistry = avatarPoolRegistry;
        }

        protected override void Update(float t)
        {
            StartAvatarLoadQuery(World);
            UpdateAvatarQuery(World);
            InstantiateAvatarQuery(World);
            DestroyAvatarQuery(World);
            UpdateAvatarBonesQuery(World);
        }

        [Query]
        private void UpdateAvatarBones(ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.IsDirty)
                return;

            avatarShapeComponent.CombinedMeshGpuSkinningComponent.ComputeSkinning(avatarShapeComponent.CompleteBoneMatrixCalculations());
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void StartAvatarLoad(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            Promise wearablePromise = CreateWearablePromise(pbAvatarShape, partition);
            pbAvatarShape.IsDirty = false;
            World.Add(entity, new AvatarShapeComponent(pbAvatarShape.Id, pbAvatarShape, wearablePromise));
        }

        private Promise CreateWearablePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition)
        {
            List<string> pointers = ListPool<string>.Get();
            pointers.Add(pbAvatarShape.BodyShape);
            pointers.AddRange(pbAvatarShape.Wearables);

            IWearable[] results = ArrayPool<IWearable>.Shared.Rent(pointers.Count);
            return Promise.Create(World, new GetWearablesByPointersIntention(pointers, results, pbAvatarShape), partition);
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
            pbAvatarShape.IsDirty = false;
        }

        [Query]
        public void InstantiateAvatar(ref AvatarShapeComponent avatarShapeComponent, ref TransformComponent transformComponent)
        {
            if (!avatarShapeComponent.IsDirty)
                return;

            if (!instantiationFrameTimeBudgetProvider.TrySpendBudget())
                return;

            if (!avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableLoadingResult<IWearable[]> wearablesResult)) return;

            AvatarBase avatarBase = avatarShapeComponent.Base ?? avatarPoolRegistry.Get();
            avatarBase.gameObject.name = $"Avatar {avatarShapeComponent.ID}";

            avatarShapeComponent.Base = avatarBase;
            avatarShapeComponent.SetupBurstJob(avatarShapeComponent.Base.transform, avatarShapeComponent.Base.AvatarSkinnedMeshRenderer.bones);

            Transform avatarTransform = avatarBase.transform;
            avatarTransform.SetParent(transformComponent.Transform, false);
            avatarTransform.ResetLocalTRS();

            ClearWearables(avatarShapeComponent.InstantiatedWearables);

            //Using Pointer size for counter, since we dont know the size of the results array
            //because it was pooled
            for (var i = 0; i < avatarShapeComponent.WearablePromise.LoadingIntention.Pointers.Count; i++)
            {
                IWearable resultWearable = wearablesResult.Asset[i];
                GameObject instantiateWearable = InstantiateWearable(ref avatarShapeComponent, resultWearable.AssetBundleData[avatarShapeComponent.BodyShape].Value.Asset.GameObject, avatarBase.AvatarSkinnedMeshRenderer, avatarTransform);

                //TODO: Do a proper hiding algorithm
                if (resultWearable.IsBodyShape())
                    HideBodyParts(instantiateWearable);
            }

            avatarShapeComponent.CombinedMeshGpuSkinningComponent = new SimpleComputeShaderSkinning();
            avatarShapeComponent.CombinedMeshGpuSkinningComponent.Initialize(avatarShapeComponent.InstantiatedWearables, avatarShapeComponent.Base.AvatarSkinnedMeshRenderer.bones);

            ListPool<string>.Release(avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            ArrayPool<IWearable>.Shared.Return(avatarShapeComponent.WearablePromise.LoadingIntention.Results);
            avatarShapeComponent.IsDirty = false;
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void DestroyAvatar(ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.Base != null)
                avatarPoolRegistry.Release(avatarShapeComponent.Base);

            ClearWearables(avatarShapeComponent.InstantiatedWearables);
        }

        private void ClearWearables(List<GameObject> instantiatedWearables)
        {
            //TODO: Pooling of wearables
            foreach (GameObject instantiatedWearable in instantiatedWearables)
                Object.Destroy(instantiatedWearable);

            instantiatedWearables.Clear();
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

        private GameObject InstantiateWearable(ref AvatarShapeComponent avatarShapeComponent, GameObject wearableToInstantiate, SkinnedMeshRenderer baseAvatar, Transform parentTransform)
        {
            //TODO: Pooling and combining of wearables
            GameObject instantiatedWearable = Object.Instantiate(wearableToInstantiate, parentTransform);
            instantiatedWearable.transform.ResetLocalTRS();

            avatarShapeComponent.InstantiatedWearables.Add(instantiatedWearable);
            return instantiatedWearable;
        }
    }
}
