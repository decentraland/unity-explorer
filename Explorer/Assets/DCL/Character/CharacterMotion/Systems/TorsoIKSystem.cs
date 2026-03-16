using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    public partial class TorsoIKSystem : BaseUnityLoopSystem
    {
        private readonly ICharacterControllerSettings localSettings;

        private TorsoIKSystem(World world,
            ICharacterControllerSettings localSettings) : base(world)
        {
            this.localSettings = localSettings;
        }

        protected override void Update(float t)
        {
            ApplyPointAtIKQuery(World, t);
        }

        [Query]
        private void ApplyPointAtIK(
            [Data] float dt,
            in HandPointAtComponent handPointAtComponent,
            ref TorsoIKComponent torsoIKComponent,
            ref AvatarBase avatarBase)
        {
            //NOTE: Right now torso IK works ONLY with Point-at feature
            torsoIKComponent.IsEnabled = handPointAtComponent.IsPointing;

            float targetAnimWeight = torsoIKComponent.IsEnabled ? 1f : 0f;

            torsoIKComponent.Weight = Mathf.MoveTowards(
                torsoIKComponent.Weight, targetAnimWeight, localSettings.IKWeightSpeed * dt);

            avatarBase.TorsoIKRig.weight = torsoIKComponent.Weight;

            if (!handPointAtComponent.IsPointing)
                return;

            float targetZ = 0f;
            Vector3 direction = (handPointAtComponent.WorldHitPoint - avatarBase.RightShoulderAnchorPoint.position).normalized;
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            float horizontalMag = horizontal.magnitude;

            if (horizontalMag > 1e-6f)
            {
                float elevation = Mathf.Atan2(direction.y, horizontalMag);
                float t = Mathf.InverseLerp(-localSettings.PointAtRotationVerticalDownThreshold, localSettings.PointAtRotationVerticalUpThreshold, elevation);
                targetZ = Mathf.Lerp(0.2f, -0.1f, t);
            }


            Vector3 localPos = avatarBase.TorsoTarget.localPosition;
            localPos.z = targetZ;
            avatarBase.TorsoTarget.localPosition = localPos;
        }
    }
}
