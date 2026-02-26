using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Conversion;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;

namespace DCL.SDKComponents.PhysicsImpulse.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))] // SyncedSimulationSystemGroup updates before we apply CharacterMotion in GlobalWorld
    [LogCategory(ReportCategory.MOTION)]
    public partial class SDKExternalPhysicsSystems : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;
        private readonly ISceneStateProvider sceneStateProvider;

        internal SDKExternalPhysicsSystems(World world, World globalWorld, Entity globalPlayerEntity, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            ApplyPhysicsForceQuery(World!);
            ApplyPhysicsImpulseQuery(World!);
        }

        [Query]
        [All(typeof(PBPhysicsCombinedForce))]
        private void ApplyPhysicsForce(in PBPhysicsCombinedForce pbPhysicsForce, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY) return;

            if (pbPhysicsForce.Vector != null)
            {
                var rigidTransform = globalWorld.Get<CharacterRigidTransform>(globalPlayerEntity);
                rigidTransform.ExternalForce += pbPhysicsForce.Vector.ToUnityVector();
            }
        }

        [Query]
        [All(typeof(PBPhysicsCombinedImpulse))]
        private void ApplyPhysicsImpulse(in PBPhysicsCombinedImpulse pbPhysicsImpulse, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY) return;

            if (pbPhysicsImpulse is { IsDirty: true, Vector: not null })
            {
                var rigidTransform = globalWorld.Get<CharacterRigidTransform>(globalPlayerEntity);
                rigidTransform.ExternalImpulse += pbPhysicsImpulse.Vector.ToUnityVector();

                pbPhysicsImpulse.IsDirty = false;
            }
        }
    }
}
