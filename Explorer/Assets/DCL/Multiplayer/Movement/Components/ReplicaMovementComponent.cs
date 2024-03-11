using Castle.Core.Internal;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct RemotePlayerMovementComponent
    {
        public const string SELF_ID = "SelfReplica";

        public readonly List<FullMovementMessage> PassedMessages;
        public readonly string PlayerWalletId;

        public RemotePlayerMovementComponent(string playerWalletId)
        {
            PlayerWalletId = playerWalletId.IsNullOrEmpty()?  SELF_ID: playerWalletId;
            PassedMessages = new List<FullMovementMessage>();
        }
    }
}
