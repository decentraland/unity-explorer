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
using ECS.LifeCycle.Components;
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
            OrphanChildrenOfDeletedEntityQuery(World);
            DoUITransformParentingQuery(World);
        }

        [Query]
        [All(typeof(PBUiTransform), typeof(UITransformComponent), typeof(DeleteEntityIntention))]
        private void OrphanChildrenOfDeletedEntity(ref UITransformComponent uiTransformComponentToBeDeleted)
        {
            foreach (EntityReference childEntity in uiTransformComponentToBeDeleted.Children)
                SetNewChild(ref World.Get<UITransformComponent>(childEntity.Entity), childEntity, sceneRoot);

            uiTransformComponentToBeDeleted.Children.Clear();
        }

        [Query]
        private void DoUITransformParenting(in Entity entity, ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            Entity parentReference = sceneRoot;

            if (entitiesMap.TryGetValue(sdkModel.Parent, out Entity newParentEntity))
            {
                parentReference = newParentEntity;

                //We have to remove the child from the old parent
                if (uiTransformComponent.RelationData.parent != parentReference)
                    RemoveFromParent(uiTransformComponent, World.Reference(entity));

                if (parentReference != sceneRoot)
                    SetNewChild(ref uiTransformComponent, World.Reference(entity), parentReference);
            }
        }

        private void SetNewChild(ref UITransformComponent childComponent, EntityReference childEntityReference, Entity parentEntity)
        {
            if (childComponent.RelationData.parent == parentEntity)
                return;

            if (!World.IsAlive(parentEntity))
            {
                ReportHub.LogError(GetReportCategory(), $"Trying to parent entity {childEntityReference.Entity} to a dead entity parent");
                return;
            }

            UITransformComponent parentComponent = World.Get<UITransformComponent>(parentEntity);

            if (parentComponent == childComponent)
                return;

            parentComponent.Transform.Add(childComponent.Transform);
            childComponent.RelationData.parent = World.Reference(parentEntity);
            parentComponent.Children.Add(childEntityReference);
        }

        private void RemoveFromParent(UITransformComponent childComponent, EntityReference childEntityReference)
        {
            if (childComponent.RelationData.parent.IsAlive(World))
                World.Get<UITransformComponent>(childComponent.RelationData.parent.Entity).Children.Remove(childEntityReference);
        }
    }
}
