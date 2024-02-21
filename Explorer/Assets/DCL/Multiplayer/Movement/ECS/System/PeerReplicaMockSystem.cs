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
    public partial class PeerReplicaMockSystem : BaseUnityLoopSystem
    {
        private readonly MessagePipeMock incomingMessages;

        private PeerReplicaMockSystem(World world, MessagePipeMock incomingMessages) : base(world)
        {
            this.incomingMessages = incomingMessages;
        }

        protected override void Update(float t)
        {
            UpdateInterpolationQuery(World);
        }

        [Query]
        private void UpdateInterpolation(ref ReplicaMovementComponent replicaMovement, ref InterpolationComponent @int, ref ExtrapolationComponent ext)
        {
            if (@int.Enabled)
                @int.Update(UnityEngine.Time.deltaTime);
            else
            {
                if (incomingMessages.Count > 0)
                {
                    if (ext.Enabled)
                    {
                        ext.Enabled = false;

                        @int.passedMessages.Add(new MessageMock
                        {
                            timestamp = ext.Start.timestamp + ext.Time,
                            position = ext.Transform.position,
                            velocity = ext.Velocity,
                            acceleration = Vector3.zero,
                        });
                    }

                    @int.Run(incomingMessages.Dequeue(), incomingMessages.Count, incomingMessages.InterpolationType);
                    @int.Update(UnityEngine.Time.deltaTime);
                }
                else
                {
                    if (ext.Enabled)
                        ext.Update(UnityEngine.Time.deltaTime);
                    else if (@int.passedMessages.Count > 1)
                    {
                        ext.Run(@int.passedMessages[^1]);
                        ext.Update(UnityEngine.Time.deltaTime);
                    }
                }
            }
        }
    }
}
