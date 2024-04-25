using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Groups;
using System.Collections.Generic;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformParentingSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformSortingSystem : BaseUnityLoopSystem
    {
        private readonly Entity sceneRoot;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        internal UITransformSortingSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, Entity sceneRoot) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            DoUITransformSortingQuery(World);
        }

        [Query]
        private void DoUITransformSorting(ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            SortUITransform(ref uiTransformComponent);

            if (uiTransformComponent.RelationData.parent == EntityReference.Null)
                return;

            foreach (EntityReference brotherEntity in World.Get<UITransformComponent>(uiTransformComponent.RelationData.parent).Children)
            {
                if (!brotherEntity.IsAlive(World))
                    continue;

                SortUITransform(ref World.Get<UITransformComponent>(brotherEntity));
            }
        }

        private void SortUITransform(ref UITransformComponent uiTransform)
        {
            if (!entitiesMap.TryGetValue(uiTransform.RightOf, out Entity entityOnLeft) || entityOnLeft == sceneRoot)
                return;

            var uiTransformOnLeft = World.Get<UITransformComponent>(entityOnLeft);

            if (uiTransform.Transform.parent != uiTransformOnLeft.Transform.parent)
                return;

            uiTransform.Transform.PlaceInFront(uiTransformOnLeft.Transform);
        }
    }
}
