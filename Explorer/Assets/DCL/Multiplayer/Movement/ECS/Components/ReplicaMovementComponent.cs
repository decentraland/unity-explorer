using DCL.Multiplayer.Movement.MessageBusMock;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct ReplicaMovementComponent
    {
        public readonly List<MessageMock> PassedMessages;

        public ReplicaMovementComponent(List<MessageMock> list)
        {
            PassedMessages = new List<MessageMock>();
        }
    }
}
