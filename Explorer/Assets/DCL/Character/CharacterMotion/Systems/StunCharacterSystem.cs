using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public partial class StunCharacterSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity time;

        private StunCharacterSystem(World world) : base(world) { }

        public override void Initialize()
        {
            time = World.CacheTime();
        }

        protected override void Update(float t)
        {
            CheckStunStatusQuery(World, time.GetTimeComponent(World).Time);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CheckStunStatus(
            [Data] float currentTime,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterController characterController,
            ref ICharacterControllerSettings characterControllerSettings,
            ref StunComponent stunComponent)
        {
            if (!stunComponent.IsStunned)
            {
                Vector3 currentPosition = characterController.transform.position;
                float deltaHeight = stunComponent.TopUngroundedHeight - currentPosition.y;

                if (rigidTransform.IsGrounded)
                {
                    stunComponent.TopUngroundedHeight = currentPosition.y;

                    if (deltaHeight > characterControllerSettings.JumpHeightStun)
                    {
                        stunComponent.LastStunnedTime = currentTime;
                        stunComponent.IsStunned = true;
                        return;
                    }
                }

                float currentVerticalVelocity = rigidTransform.GravityVelocity.y;

                if (stunComponent.LastVerticalVelocity >= 0 && currentVerticalVelocity < 0)
                    stunComponent.TopUngroundedHeight = currentPosition.y;

                stunComponent.LastVerticalVelocity = currentVerticalVelocity;
            }
            else
            {
                float timeStunned = currentTime - stunComponent.LastStunnedTime;

                if (timeStunned >= characterControllerSettings.LongFallStunTime)
                    stunComponent.IsStunned = false;
            }
        }
    }
}
