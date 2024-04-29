using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using System;

namespace DCL.CharacterTriggerArea.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))] // Throttling enabled for the group itself
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class CharacterTriggerAreaCleanupSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<CharacterTriggerArea> poolRegistry;

        public CharacterTriggerAreaCleanupSystem(World world, IComponentPool<CharacterTriggerArea> poolRegistry) : base(world)
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
        private void HandleEntityDestruction(Entity entity, ref CharacterTriggerAreaComponent component)
        {
            poolRegistry.Release(component.MonoBehaviour);

            // For some reason bulk deletion shows very bad performance (probably due to the total number of archetypes/chunks)
            World.Remove<CharacterTriggerAreaComponent>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBCameraModeArea), typeof(PBAvatarModifierArea))]
        private void HandleComponentRemoval(Entity entity, ref CharacterTriggerAreaComponent component)
        {
            poolRegistry.Release(component.MonoBehaviour);
            World.Remove<CharacterTriggerAreaComponent>(entity);
        }

        [Query]
        private void FinalizeComponents(ref CharacterTriggerAreaComponent component)
        {
            poolRegistry.Release(component.MonoBehaviour);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
