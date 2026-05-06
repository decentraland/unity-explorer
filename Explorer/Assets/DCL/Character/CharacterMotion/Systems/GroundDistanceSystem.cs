using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.Utilities;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(ChangeCharacterPositionGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public partial class GroundDistanceSystem : BaseUnityLoopSystem
    {
        private const float GROUND_CHECK_RADIUS = 0.1f;
        private const float MAX_GROUND_DISTANCE = 1000;
        private Ray checkRay = new (Vector3.zero, Vector3.down);

        public GroundDistanceSystem(World world) : base(world)
        {
        }

        protected override void Update(float t) =>
            CalculateGroundDistanceQuery(World);

        [Query]
        private void CalculateGroundDistance(in IAvatarView avatarView, ref CharacterRigidTransform rigidTransform)
        {
            Vector3 groundCheckOrigin = avatarView.GetTransform().position + ((GROUND_CHECK_RADIUS + 0.001f) * Vector3.up);
            checkRay.origin = groundCheckOrigin;

            //QueryTriggerInteraction.Ignore prevents trigger areas to be detected in the sphere cast, detecting only concrete colliders and avoid randomly closing the glider
            bool didHit = DCLPhysics.SphereCast(checkRay, GROUND_CHECK_RADIUS, out RaycastHit hit, Mathf.Infinity, PhysicsLayers.CHARACTER_ONLY_MASK, QueryTriggerInteraction.Ignore);

            rigidTransform.GroundDistance = didHit ? hit.distance : MAX_GROUND_DISTANCE;
        }
    }
}
