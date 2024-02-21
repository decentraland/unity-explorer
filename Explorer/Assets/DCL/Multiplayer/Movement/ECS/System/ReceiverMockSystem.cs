using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.MessageBusMock;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InstantiateRandomAvatarsSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class ReceiverMockSystem : BaseUnityLoopSystem
    {
        private const float MIN_POSITION_DELTA = 0.1f;
        private readonly MessagePipeMock incomingMessages;

        private ReceiverMockSystem(World world, MessagePipeMock incomingMessages) : base(world)
        {
            this.incomingMessages = incomingMessages;
        }

        protected override void Update(float t)
        {
            UpdateInterpolationQuery(World);
        }

        [Query]
        private void UpdateInterpolation(ref ReplicaMovementComponent replicaMovement, ref InterpolationComponent @int, ref ExtrapolationComponent ext, ref BlendComponent blend)
        {
            if (@int.Enabled)
                @int.Update(UnityEngine.Time.deltaTime);
            else
            {
                if (incomingMessages.Count != 0)
                {
                    if (ext.Enabled)
                    {
                        @int.PassedMessages.Add(ext.Stop());

                        // MessageMock? local = ext.Stop();
                        // MessageMock? remote = incomingMessages.Dequeue();
                        //
                        // if (Vector3.Distance(local.position, remote.position) < MIN_POSITION_DELTA)
                        //     blend.Run(local, remote);
                        // else
                        //     @int.PassedMessages.Add(remote);
                    }

                    // if (blend.Enabled)
                    // {
                    //     (MessageMock startedRemote, MessageMock extra) = blend.Update(UnityEngine.Time.deltaTime);
                    //
                    //     if (blend.Enabled) return;
                    //
                    //     @int.PassedMessages.Add(startedRemote);
                    //     if (extra != null) @int.PassedMessages.Add(extra);
                    // }

                    MessageMock? start = @int.PassedMessages.Count > 0 ? @int.PassedMessages[^1] : null;
                    @int.Run(start, incomingMessages.Dequeue(), incomingMessages.Count, incomingMessages.InterpolationType);
                    @int.Update(UnityEngine.Time.deltaTime);
                }
                else
                {
                    if (ext.Enabled)
                        ext.Update(UnityEngine.Time.deltaTime);
                    else if (@int.PassedMessages.Count > 1)
                    {
                        ext.Run(@int.PassedMessages[^1]);
                        ext.Update(UnityEngine.Time.deltaTime);
                    }
                }
            }
        }
    }
}
