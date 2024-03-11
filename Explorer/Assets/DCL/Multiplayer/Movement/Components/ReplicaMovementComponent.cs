namespace DCL.Multiplayer.Movement
{
    public struct RemotePlayerMovementComponent
    {
        public const string TEST_ID = "SelfReplica";

        public readonly string PlayerWalletId;

        public FullMovementMessage PastMessage;

        public bool Initialized;
        public bool WasTeleported;

        public RemotePlayerMovementComponent(string playerWalletId)
        {
            PlayerWalletId = playerWalletId;

            PastMessage = new FullMovementMessage();
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
