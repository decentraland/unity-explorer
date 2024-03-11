using DCL.Multiplayer.Movement.ECS;
using JetBrains.Annotations;

namespace DCL.Multiplayer.Movement
{
    public struct RemotePlayerMovementComponent
    {
        public const string TEST_ID = "SelfReplica";

        public readonly string PlayerWalletId;
        [CanBeNull] public FullMovementMessage PastMessage;

        public bool Initialized;
        public bool WasTeleported;

        public RemotePlayerMovementComponent(string playerWalletId)
        {
            PlayerWalletId = playerWalletId;

            PastMessage = null;
            Initialized = false;
            WasTeleported = false;
        }

        public void AddPassed(FullMovementMessage message, bool wasTeleported = false)
        {
            PastMessage = message;
            WasTeleported = wasTeleported;
        }
    }
}
