using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Conversion;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using UnityEngine;

namespace DCL.SDKComponents.PhysicsImpulse.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class PhysicsImpulseSystems : BaseUnityLoopSystem
    {
        private const float MAX_EXTERNAL_VELOCITY = 50f;

        private readonly World globalWorld;
        private readonly Entity globalPlayerEntity;

        internal PhysicsImpulseSystems(World world, World globalWorld, Entity globalPlayerEntity) : base(world)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
        }

        protected override void Update(float t)
        {
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

            Vector3 impulseVelocity = pbPhysicsImpulse.Direction.ToUnityVector();

            var rigidTransform = globalWorld.Get<CharacterRigidTransform>(globalPlayerEntity);
            rigidTransform.ExternalVelocity += impulseVelocity;

            // Clamp to max external velocity
            if (rigidTransform.ExternalVelocity.sqrMagnitude > MAX_EXTERNAL_VELOCITY * MAX_EXTERNAL_VELOCITY)
                rigidTransform.ExternalVelocity = rigidTransform.ExternalVelocity.normalized * MAX_EXTERNAL_VELOCITY;

            // Unground the character if impulse has upward component
            if (impulseVelocity.y > 0)
                rigidTransform.IsGrounded = false;

            pbPhysicsImpulse.IsDirty = false;
        }
    }
}
