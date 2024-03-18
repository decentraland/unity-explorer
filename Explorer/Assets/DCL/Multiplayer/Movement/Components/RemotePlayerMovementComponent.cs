namespace DCL.Multiplayer.Movement
{
    public struct RemotePlayerMovementComponent
    {
        public const string TEST_ID = "SelfReplica";

        public readonly string PlayerWalletId;

        public NetworkMovementMessage PastMessage;

        public bool Initialized;
        public bool WasTeleported;
        public bool RequireAnimationsUpdate;

        public RemotePlayerMovementComponent(string playerWalletId)
        {
            PlayerWalletId = playerWalletId;

            PastMessage = new NetworkMovementMessage();
            Initialized = false;
            WasTeleported = false;

            RequireAnimationsUpdate = false;
        }

        public void AddPassed(NetworkMovementMessage message, bool wasTeleported = false)
        {
            PastMessage = message;
            WasTeleported = wasTeleported;

            RequireAnimationsUpdate = true;
        }
    }
}
