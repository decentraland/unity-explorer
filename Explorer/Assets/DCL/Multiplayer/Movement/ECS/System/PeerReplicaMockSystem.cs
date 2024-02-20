using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InstantiateRandomAvatarsSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class PeerReplicaMockSystem : BaseUnityLoopSystem
    {
        private readonly MessagePipeMock pipeMock;

        private PeerReplicaMockSystem(World world, MessagePipeMock pipeMock) : base(world)
        {
            this.pipeMock = pipeMock;
        }

        protected override void Update(float t)
        {
            UpdateInterpolationQuery(World);
        }

        [Query]
        private void UpdateInterpolation(ref InterpolationComponent interpolation)
        {
            if (pipeMock.IncomingMessages.Count > 0 && !interpolation.isInterpolating)
            {
                interpolation.StartInterpolate(
                    pipeMock.IncomingMessages.Dequeue(), pipeMock.InterpolationType);
            }
            else
            {
                interpolation.Update(UnityEngine.Time.deltaTime);
            }
        }
    }
}
