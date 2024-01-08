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
    [UpdateAfter(typeof(UITransformInstantiationSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformParentingSystem : BaseUnityLoopSystem
    {
        private readonly Entity sceneRoot;
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        private UITransformParentingSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, Entity sceneRoot) : base(world)
        {
            this.sceneRoot = sceneRoot;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            DoUITransformParentingQuery(World);
        }

        [Query]
        [All(typeof(PBUiTransform), typeof(UITransformComponent))]
        private void DoUITransformParenting(in Entity entity, ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            if (entitiesMap.TryGetValue(sdkModel.Parent, out Entity newParentEntity) && newParentEntity != sceneRoot)
            {
                SetNewChild(ref uiTransformComponent, World.Reference(entity), newParentEntity);
                uiTransformComponent.RightOf = sdkModel.RightOf;
            }
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
            parentComponent.Children.Add(childEntityReference);
            childComponent.Parent = World.Reference(parentEntity);
        }
    }
}
