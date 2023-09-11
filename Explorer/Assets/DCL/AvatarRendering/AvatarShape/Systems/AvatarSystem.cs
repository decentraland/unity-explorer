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
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.Transforms.Components;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.WearableDTO[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableDTOByPointersIntention>;

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
            //TODO: Release flow
            //TODO: Cancel flow
            StartAvatarLoadQuery(World);
            InstantiateAvatarQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void StartAvatarLoad(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            List<string> pointers = ListPool<string>.Get();
            pointers.Add(pbAvatarShape.BodyShape);
            pointers.AddRange(pbAvatarShape.Wearables);

            Wearable[] results = ArrayPool<Wearable>.Shared.Rent(pointers.Count);

            var werablePromise = AssetPromise<Wearable[], GetWearablesByPointersIntention>.Create(World,
                new GetWearablesByPointersIntention
                {
                    Pointers = pointers,
                    BodyShape = pbAvatarShape,
                    Results = results,
                }, partition);

            World.Add(entity, new AvatarShapeComponent(pbAvatarShape.Id, pbAvatarShape, werablePromise));
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

            //Using Pointer size for counter, since we dont know the size of the results array
            //because it was pooled
            for (var i = 0; i < avatarShapeComponent.WearablePromise.LoadingIntention.Pointers.Count; i++)
            {
                Wearable resultWearable = wearablesResult.Asset[i];

                GameObject instantiateWearable = InstantiateWearable(resultWearable.AssetBundleData[avatarShapeComponent.BodyShape].Value.Asset.GameObject, avatarBase.AvatarSkinnedMeshRenderer, avatarTransform);

                //TODO: Do a proper hiding algorithm
                if (resultWearable.IsBodyShape())
                    HideBodyParts(instantiateWearable);
            }

            ListPool<string>.Release(avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            ArrayPool<Wearable>.Shared.Return(avatarShapeComponent.WearablePromise.LoadingIntention.Results);
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
