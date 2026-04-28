namespace DCL.Multiplayer.Movement
{
    /// <summary>
    ///     Mirrors the local player's current facial expression indices so
    ///     <see cref="Systems.PlayerMovementNetSendSystem"/> can include them in every movement
    ///     packet without taking a dependency on the AvatarShape assembly.
    /// </summary>
    public struct LocalPlayerFacialExpressionComponent
    {
        public byte EyebrowsIndex;
        public byte EyesIndex;
        public byte MouthIndex;
    }
}