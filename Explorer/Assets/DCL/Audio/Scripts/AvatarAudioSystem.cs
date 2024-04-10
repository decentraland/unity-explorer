using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.Audio.Systems
{
    [LogCategory(ReportCategory.AUDIO)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class AvatarAudioSystem : BaseUnityLoopSystem
    {

        private AvatarAudioSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
           //MonitorIKDataAndSendAudioEventsQuery(World);
        }

        [Query]
        private void MonitorIKDataAndSendAudioEvents(
            ref AvatarAudioPlaybackController1 audioPlaybackController,
            in FeetIKComponent feetIKComponent,
            in CharacterRigidTransform rigidTransform
        )
        {
            if (feetIKComponent.IsDisabled) return;

            if (rigidTransform.JustJumped) { audioPlaybackController.OnJumpStart(); }
        }
    }
}
