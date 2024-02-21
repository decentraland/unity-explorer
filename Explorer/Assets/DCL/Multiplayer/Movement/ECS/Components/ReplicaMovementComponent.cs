using DCL.Multiplayer.Movement.MessageBusMock;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct ReplicaMovementComponent
    {
        public List<MessageMock> passedMessages;

        public ReplicaMovementComponent(List<MessageMock> _ = null)
        {
            this.passedMessages = new List<MessageMock>();
        }
    }
}
