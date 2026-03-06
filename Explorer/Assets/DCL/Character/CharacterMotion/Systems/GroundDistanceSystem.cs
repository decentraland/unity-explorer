using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
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

        public GroundDistanceSystem(World world) : base(world)
        {
        }

        protected override void Update(float t) =>
            CalculateGroundDistanceQuery(World);

        [Query]
        private void CalculateGroundDistance(in IAvatarView avatarView, ref CharacterRigidTransform rigidTransform)
        {
            Vector3 groundCheckOrigin = avatarView.GetTransform().position + ((GROUND_CHECK_RADIUS + 0.001f) * Vector3.up);
            LayerMask layerMask = PhysicsLayers.CHARACTER_ONLY_MASK;

            bool didHit = Physics.SphereCast(groundCheckOrigin, GROUND_CHECK_RADIUS, Vector3.down, out RaycastHit hit, layerMask);
            rigidTransform.GroundDistance = didHit ? hit.distance : MAX_GROUND_DISTANCE;
        }
    }
}
