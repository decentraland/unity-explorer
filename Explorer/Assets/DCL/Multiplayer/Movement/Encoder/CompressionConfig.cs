namespace DCL.Multiplayer.Movement
{
    public static class CompressionConfig
    {
        // 17 + 2 + 7 = 26
        public const float TIMESTAMP_QUANTUM = 0.01f;
        public const int TIMESTAMP_BITS = 18;

        // Animations
        private const int MOVEMENT_KIND_BITS = 2;
        public const int MOVEMENT_KIND_MASK = 0x3;

        public const int MOVEMENT_KIND_START_BIT = TIMESTAMP_BITS;
        public const int SLIDING_BIT = MOVEMENT_KIND_START_BIT + MOVEMENT_KIND_BITS;
        public const int STUNNED_BIT = SLIDING_BIT + 1;
        public const int GROUNDED_BIT = STUNNED_BIT + 1;
        public const int JUMPING_BIT = GROUNDED_BIT + 1;
        public const int LONG_JUMP_BIT = JUMPING_BIT + 1;
        public const int FALLING_BIT = LONG_JUMP_BIT + 1;
        public const int LONG_FALL_BIT = FALLING_BIT + 1;

        // 17 + 8 + 8 + 8 + 8 + 8 + 8 = 64
        public const int PARCEL_BITS = 17;

        // 8 + 8 + 13 + 6 + 6 + 6 = 47
        public const int XZ_BITS = 9;

        public const int Y_MAX = 500;
        public const int Y_BITS = 13;

        public const int MAX_VELOCITY = 40;
        public const int VELOCITY_BITS = 1;
    }
}
