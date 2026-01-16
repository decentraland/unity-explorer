using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using ECS.Abstract;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DCL.JumpIndicator
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class JumpIndicatorSystem : BaseUnityLoopSystem
    {
        private static readonly int MAX_GROUND_DISTANCE_ID = Shader.PropertyToID("_MaxGroundDistance");
        private static readonly int PLAYER_GROUND_DISTANCE_ID = Shader.PropertyToID("_PlayerGroundDistance");

        private readonly Entity playerEntity;
        private readonly GameObject jumpIndicatorPrefab;
        private readonly float groundCheckRadius;

        private JumpIndicatorSystem(World world, Entity playerEntity, GameObject jumpIndicatorPrefab, float groundCheckRadius) : base(world)
        {
            this.playerEntity = playerEntity;
            this.jumpIndicatorPrefab = jumpIndicatorPrefab;
            this.groundCheckRadius = groundCheckRadius;
        }

        protected override void Update(float t)
        {
            InitializeJumpIndicatorQuery(World);
            UpdateJumpIndicatorQuery(World);
        }

        [Query]
        [All(typeof(CharacterController))]
        [None(typeof(JumpIndicator))]
        private void InitializeJumpIndicator(in IAvatarView avatarView)
        {
            var jumpIndicator = Object.Instantiate(jumpIndicatorPrefab, avatarView.GetTransform());
            var decalProjector = jumpIndicator.GetComponent<DecalProjector>();

            World.Add(playerEntity, new JumpIndicator { DecalProjector = decalProjector });
        }

        [Query]
        private void UpdateJumpIndicator(in IAvatarView avatarView, in JumpIndicator jumpIndicator, in CharacterRigidTransform transform)
        {
            DecalProjector decalProjector = jumpIndicator.DecalProjector;

            decalProjector.enabled = !transform.IsGrounded;
            if (!decalProjector.enabled) return;

            Vector3 groundCheckOrigin = avatarView.GetTransform().position + ((groundCheckRadius + 0.001f) * Vector3.up);
            float maxDistance = decalProjector.size.z;
            LayerMask layerMask = PhysicsLayers.CHARACTER_ONLY_MASK;

            bool didHit = Physics.SphereCast(groundCheckOrigin, groundCheckRadius, Vector3.down, out RaycastHit hit, maxDistance, layerMask);
            float groundDistance = didHit ? hit.distance : maxDistance;

            decalProjector.material.SetFloat(MAX_GROUND_DISTANCE_ID, maxDistance);
            decalProjector.material.SetFloat(PLAYER_GROUND_DISTANCE_ID, groundDistance);
        }


        private struct JumpIndicator
        {
            public DecalProjector DecalProjector;
        }
    }
}
