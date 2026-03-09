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
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.PhysicsImpulse.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class SDKExternalPhysicsSystems : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
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

            var rigidTransform = globalWorld.Get<CharacterRigidTransform>(globalPlayerEntity);
            rigidTransform.ExternalForce = Vector3.zero;

            ApplyPhysicsForceQuery(World!, rigidTransform);
            ApplyPhysicsImpulseQuery(World!, rigidTransform);
        }

        public void OnSceneIsCurrentChanged(bool value)
        {
            if (!value)
                ResetExternalForce();
        }

        public void FinalizeComponents(in Query query) =>
            ResetExternalForce();

        private void ResetExternalForce()
        {
            var rigidTransform = globalWorld.Get<CharacterRigidTransform>(globalPlayerEntity);
            rigidTransform.ExternalForce = Vector3.zero;
            rigidTransform.ExternalAcceleration = Vector3.zero;
        }

        [Query]
        [All(typeof(PBPhysicsCombinedForce))]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyPhysicsForce([Data] CharacterRigidTransform rigidTransform, in PBPhysicsCombinedForce pbPhysicsForce, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY || pbPhysicsForce.Vector == null) return;

            rigidTransform.ExternalForce = pbPhysicsForce.Vector.ToUnityVector();
        }

        [Query]
        [All(typeof(PBPhysicsCombinedImpulse))]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyPhysicsImpulse([Data] CharacterRigidTransform rigidTransform, in PBPhysicsCombinedImpulse pbPhysicsImpulse, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY) return;

            if (pbPhysicsImpulse is { IsDirty: true, Vector: not null })
            {
                rigidTransform.ExternalImpulse += pbPhysicsImpulse.Vector.ToUnityVector();
                pbPhysicsImpulse.IsDirty = false;
            }
        }
    }
}
