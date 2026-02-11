using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Conversion;
using DCL.CharacterMotion;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.PhysicsImpulse.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class SDKPhysicsImpulseSystems : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;
        private readonly ISceneStateProvider sceneStateProvider;

        private SingleInstanceEntity characterSettings;

        internal SDKPhysicsImpulseSystems(World world, World globalWorld, Entity globalPlayerEntity, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.sceneStateProvider = sceneStateProvider;
        }

        public override void Initialize()
        {
            characterSettings = globalWorld.CacheCharacterSettings();
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            ApplyPhysicsImpulseQuery(World!);
        }

        [Query]
        [All(typeof(PBPhysicsImpulse))]
        private void ApplyPhysicsImpulse(in PBPhysicsImpulse pbPhysicsImpulse, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY) return;
            if (pbPhysicsImpulse.IsDirty == false) return;

            if (pbPhysicsImpulse.Direction == null)
            {
                pbPhysicsImpulse.IsDirty = false;
                return;
            }

            ICharacterControllerSettings settings = characterSettings.GetCharacterSettings(globalWorld);

            // Δv = impulse / mass
            Vector3 impulseVelocity = pbPhysicsImpulse.Direction.ToUnityVector() / settings.CharacterMass;

            var rigidTransform = globalWorld.Get<CharacterRigidTransform>(globalPlayerEntity);
            rigidTransform.ExternalVelocity += impulseVelocity;

            // Clamp to max external velocity
            float maxVelocity = settings.MaxExternalVelocity;

            if (rigidTransform.ExternalVelocity.sqrMagnitude > maxVelocity * maxVelocity)
                rigidTransform.ExternalVelocity = rigidTransform.ExternalVelocity.normalized * maxVelocity;

            // Unground the character if impulse has upward component
            if (impulseVelocity.y > 0)
                rigidTransform.IsGrounded = false;

            pbPhysicsImpulse.IsDirty = false;
        }
    }
}
