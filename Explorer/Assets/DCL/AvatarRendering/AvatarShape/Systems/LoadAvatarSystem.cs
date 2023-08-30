using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
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
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(LoadWearableSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class LoadAvatarSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity wearableCatalog;

        public LoadAvatarSystem(World world) : base(world) { }

        public override void Initialize()
        {
            base.Initialize();
            wearableCatalog = World.CacheWearableCatalog();
        }

        protected override void Update(float t)
        {
            StartAvatarLoadQuery(World);
            InstantiateAvatarQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void StartAvatarLoad(in Entity entity, ref PBAvatarShape pbAvatarShape)
        {
            var avatarShapeComponent = new AvatarShapeComponent
            {
                ID = pbAvatarShape.Id,
            };

            World.Add(entity, avatarShapeComponent);

            var missingWearables = new List<string>();

            if (AreWearablesReady(ref pbAvatarShape, out missingWearables)) { SetAvatarWearables(ref pbAvatarShape, ref avatarShapeComponent); }
            else
            {
                avatarShapeComponent.WearablePromise = AssetPromise<WearableDTO[], GetWearableByPointersIntention>.Create(World,
                    new GetWearableByPointersIntention
                    {
                        Pointers = missingWearables.ToArray(),
                    }, PartitionComponent.TOP_PRIORITY);
            }
        }

        [Query]
        private void FinalizeAvatarLoad(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.Status == AvatarShapeComponent.LifeCycle.LoadingWearables &&
                avatarShapeComponent.WearablePromise.TryConsume(World, out StreamableLoadingResult<WearableDTO[]> result))
            {
                if (!result.Succeeded)
                    ReportHub.LogError(GetReportCategory(), "Error loading wearables for avatar: " + avatarShapeComponent.ID);
                else
                    SetAvatarWearables(ref pbAvatarShape, ref avatarShapeComponent);
            }
        }

        private void SetAvatarWearables(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShape)
        {
            avatarShape.BodyShape = GetEntityReference(pbAvatarShape.BodyShape);

            for (var i = 0; i < pbAvatarShape.Wearables.Count; i++)
                avatarShape.Wearables[i] = GetEntityReference(pbAvatarShape.Wearables[i]);

            avatarShape.Status = AvatarShapeComponent.LifeCycle.LoadingAssetBundles;
        }

        [Query]
        private void InstantiateAvatar(ref AvatarShapeComponent avatarShape)
        {
            if (avatarShape.Loaded)
                return;

            //TODO: Handle cancel loop.
            foreach (EntityReference avatarShapeWearable in avatarShape.Wearables)
                if (!IsWearableReady(avatarShapeWearable))
                    return;

            if (!IsWearableReady(avatarShape.BodyShape))
                return;

            SkinnedMeshRenderer baseAvatar =
                Object.Instantiate(Resources.Load<GameObject>("AvatarBase")).GetComponentInChildren<SkinnedMeshRenderer>();

            InstantiateWearable(World.Get<AssetBundleData>(avatarShape.BodyShape).GameObject, baseAvatar);

            for (var i = 0; i < avatarShape.Wearables.Length; i++)
                InstantiateWearable(World.Get<AssetBundleData>(avatarShape.Wearables[i]).GameObject, baseAvatar);

            avatarShape.Loaded = true;
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
            wearableCatalog.GetWearableCatalog(World).catalog.TryGetValue(urnToAnalyze, out EntityReference state);

        private EntityReference GetEntityReference(string urnToAnalyze) =>
            wearableCatalog.GetWearableCatalog(World).catalog[urnToAnalyze];

        private bool IsWearableReady(EntityReference wearableEntityReference) =>
            World.Get<WearableComponent>(wearableEntityReference).Status == WearableComponent.LifeCycle.LoadingFinished;
    }
}
