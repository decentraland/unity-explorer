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

            ApplyIKWeightQuery(World);
        }

        [Query]
        private void ApplyIKWeight(
            in TorsoIKComponent torsoIKComponent,
            ref AvatarBase avatarBase)
        {
            avatarBase.TorsoIKRig.weight = torsoIKComponent.Weight;
        }

        [Query]
        private void ApplyPointAtIK(
            [Data] float dt,
            in HandPointAtComponent handPointAtComponent,
            ref TorsoIKComponent torsoIKComponent,
            ref AvatarBase avatarBase)
        {
            torsoIKComponent.IsEnabled = handPointAtComponent.IsPointing;

            float targetAnimWeight = torsoIKComponent.IsEnabled ? 1f : 0f;

            torsoIKComponent.Weight = Mathf.MoveTowards(
                torsoIKComponent.Weight, targetAnimWeight, localSettings.IKWeightSpeed * dt);
        }
    }
}
