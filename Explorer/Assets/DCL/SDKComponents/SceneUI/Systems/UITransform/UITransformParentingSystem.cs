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
            var children = uiTransformComponentToBeDeleted.RelationData.Children;
            if (children == null) return;

            foreach (EntityReference childEntity in children)
            {
                ref UITransformComponent uiTransform = ref World.TryGetRef<UITransformComponent>(childEntity.Entity, out bool exists);

                if (!exists)
                {
                    ReportHub.LogError(GetReportCategory(), $"Trying to unparent an ${nameof(UITransformComponent)}'s child but no component has been found on entity {childEntity.Entity}");
                    continue;
                }

                uiTransformComponentToBeDeleted.RelationData.RemoveChild(ref uiTransform.RelationData);
                SetNewChild(ref uiTransform, childEntity, sceneRoot);
            }
        }

        [Query]
        private void DoUITransformParenting(in Entity entity, ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            if (entitiesMap.TryGetValue(sdkModel.Parent, out Entity newParentEntity))
            {
                Entity parentReference = newParentEntity;

                //We have to remove the child from the old parent
                if (uiTransformComponent.RelationData.parent != parentReference)
                    RemoveFromParent(uiTransformComponent);

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

            ref UITransformComponent parentComponent = ref World.TryGetRef<UITransformComponent>(parentEntity, out bool exists);

            if (!exists)
            {
                ReportHub.LogError(GetReportCategory(), $"Trying to parent entity {childEntityReference.Entity} to a parent {parentEntity} that do not have ${nameof(UITransformComponent)} component");
                return;
            }

            if (parentComponent == childComponent) return;

            parentComponent.RelationData.AddChild(World.Reference(parentEntity), childEntityReference, ref childComponent.RelationData);
            parentComponent.Transform.Add(childComponent.Transform);
        }

        private void RemoveFromParent(UITransformComponent childComponent)
        {
            if (!childComponent.RelationData.parent.IsAlive(World)) return;

            ref UITransformComponent parentTransform = ref World.TryGetRef<UITransformComponent>(childComponent.RelationData.parent.Entity, out bool exists);

            if (!exists)
            {
                ReportHub.LogError(GetReportCategory(), $"Trying to remove a child from a parent {childComponent.RelationData.parent.Entity} that do not have ${nameof(UITransformComponent)} component");
                return;
            }

            parentTransform.RelationData.RemoveChild(ref childComponent.RelationData);
        }
    }
}
