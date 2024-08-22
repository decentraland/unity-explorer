using System;

namespace DCL.Multiplayer.Movement
{
    [Serializable]
    public struct CompressedNetworkMovementMessage
    {
        public int temporalData;
        public long movementData;
        public NetworkMovementMessage original;
    }
}
