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

namespace DCL.SDKComponents.SceneUI.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformInstantiationSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformParentingSystem : BaseUnityLoopSystem
    {
        private readonly Entity sceneRoot;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        public UITransformParentingSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, Entity sceneRoot) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            DoParentingQuery(World);
        }

        [Query]
        [All(typeof(PBUiTransform), typeof(UITransformComponent))]
        private void DoParenting(in Entity entity, ref PBUiTransform sdkTransform, ref UITransformComponent transformComponent)
        {
            if (!sdkTransform.IsDirty)
                return;

            if (entitiesMap.TryGetValue(sdkTransform.Parent, out Entity newParentEntity) &&
                newParentEntity != sceneRoot &&
                !transformComponent.Transform.Contains(World.Get<UITransformComponent>(newParentEntity).Transform)) // TODO: This check shouldn't be needed!
                SetNewChild(ref transformComponent, World.Reference(entity), newParentEntity);
        }

        private void SetNewChild(ref UITransformComponent childComponent, EntityReference childEntityReference, Entity parentEntity)
        {
            if (childComponent.Parent == parentEntity)
                return;

            if (!World.IsAlive(parentEntity))
            {
                ReportHub.LogError(GetReportCategory(), $"Trying to parent entity {childEntityReference.Entity} to a dead entity parent");
                return;
            }

            UITransformComponent parentComponent = World.Get<UITransformComponent>(parentEntity);
            parentComponent.Transform.Add(childComponent.Transform);
            childComponent.Parent = World.Reference(parentEntity);
        }
    }
}
