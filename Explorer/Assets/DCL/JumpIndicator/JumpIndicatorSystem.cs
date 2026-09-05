using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
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
        private readonly DecalProjector jumpIndicatorPrefab;

        private JumpIndicatorSystem(World world, Entity playerEntity, DecalProjector jumpIndicatorPrefab) : base(world)
        {
            this.playerEntity = playerEntity;
            this.jumpIndicatorPrefab = jumpIndicatorPrefab;
        }

        protected override void Update(float t)
        {
            InitializeJumpIndicatorQuery(World);
            UpdateJumpIndicatorQuery(World);
        }

        [Query]
        [All(typeof(CharacterController))]
        [None(typeof(DeleteEntityIntention), typeof(JumpIndicator))]
        private void InitializeJumpIndicator(in IAvatarView avatarView)
        {
            var decalProjector = Object.Instantiate(jumpIndicatorPrefab, avatarView.GetTransform());

            // Copy the material to avoid modifying the original asset
            decalProjector.material = new Material(decalProjector.material);

            World.Add(playerEntity, new JumpIndicator { DecalProjector = decalProjector });
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateJumpIndicator(in JumpIndicator jumpIndicator, in CharacterRigidTransform transform)
        {
            DecalProjector decalProjector = jumpIndicator.DecalProjector;

            decalProjector.enabled = !transform.IsGrounded;
            if (!decalProjector.enabled) return;

            decalProjector.material.SetFloat(MAX_GROUND_DISTANCE_ID, decalProjector.size.z);
            decalProjector.material.SetFloat(PLAYER_GROUND_DISTANCE_ID, transform.GroundDistance);
        }


        private struct JumpIndicator
        {
            public DecalProjector DecalProjector;
        }
    }
}
