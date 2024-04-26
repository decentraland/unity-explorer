using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;

namespace DCL.CharacterTriggerArea.Systems
{
    [UpdateInGroup(typeof(SyncedPostPhysicsSystemGroup))]
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
            ClearDetectedCharactersCollectionQuery(World!);

            HandleEntityDestructionQuery(World!);
            World!.Remove<CharacterTriggerAreaComponent>(HandleEntityDestruction_QueryDescription);

            HandleComponentRemovalQuery(World!);
            World!.Remove<CharacterTriggerAreaComponent>(HandleComponentRemoval_QueryDescription);
        }

        [Query]
        private void ClearDetectedCharactersCollection(ref CharacterTriggerAreaComponent component)
        {
            component.TryClear();
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref CharacterTriggerAreaComponent component)
        {
            component.TryRelease(poolRegistry);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBCameraModeArea), typeof(PBAvatarModifierArea))]
        private void HandleComponentRemoval(ref CharacterTriggerAreaComponent component)
        {
            component.TryRelease(poolRegistry);
        }

        [Query]
        private void FinalizeComponents(in Entity entity, ref CharacterTriggerAreaComponent component)
        {
            component.TryRelease(poolRegistry);
            World.Remove<CharacterTriggerAreaComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
