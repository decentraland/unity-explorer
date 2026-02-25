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
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
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
        [All(typeof(PBPhysicsTotalForce))]
        private void ApplyPhysicsForce(in PBPhysicsTotalForce pbPhysicsForce, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY) return;
            if (pbPhysicsForce.Vector == null) return;

            var rigidTransform = globalWorld.Get<CharacterRigidTransform>(globalPlayerEntity);
            rigidTransform.ExternalForce += pbPhysicsForce.Vector.ToUnityVector();
        }

        [Query]
        [All(typeof(PBPhysicsTotalImpulse))]
        private void ApplyPhysicsImpulse(in PBPhysicsTotalImpulse pbPhysicsImpulse, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY) return;
            if (pbPhysicsImpulse.IsDirty == false) return;

            if (pbPhysicsImpulse.Vector == null)
            {
                pbPhysicsImpulse.IsDirty = false;
                return;
            }

            var rigidTransform = globalWorld.Get<CharacterRigidTransform>(globalPlayerEntity);
            rigidTransform.ExternalImpulse += pbPhysicsImpulse.Vector.ToUnityVector();

            pbPhysicsImpulse.IsDirty = false;
        }
    }
}
