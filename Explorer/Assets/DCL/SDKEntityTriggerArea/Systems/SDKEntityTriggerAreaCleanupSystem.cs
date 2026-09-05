using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.SDKEntityTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;

namespace DCL.SDKEntityTriggerArea.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))] // Throttling enabled for the group itself
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class SDKEntityTriggerAreaCleanupSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<SDKEntityTriggerArea> poolRegistry;

        public SDKEntityTriggerAreaCleanupSystem(World world, IComponentPool<SDKEntityTriggerArea> poolRegistry) : base(world)
        {
            this.poolRegistry = poolRegistry;
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(Entity entity, ref SDKEntityTriggerAreaComponent component)
        {
            component.TryRelease(poolRegistry);

            // For some reason bulk deletion shows very bad performance (probably due to the total number of archetypes/chunks)
            World.Remove<SDKEntityTriggerAreaComponent>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBCameraModeArea), typeof(PBAvatarModifierArea), typeof(PBTriggerArea))]
        private void HandleComponentRemoval(Entity entity, ref SDKEntityTriggerAreaComponent component)
        {
            component.TryRelease(poolRegistry);
            World.Remove<SDKEntityTriggerAreaComponent>(entity);
        }

        [Query]
        private void FinalizeComponents(ref SDKEntityTriggerAreaComponent component)
        {
            component.TryRelease(poolRegistry);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
