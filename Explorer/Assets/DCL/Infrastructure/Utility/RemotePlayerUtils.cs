namespace DCL.Utilities
{
    public static class RemotePlayerUtils
    {
        public const float JUMP_EPSILON = 0.01f;
        public const float MOVEMENT_EPSILON = 0.01f;
        public const float TIME_EPSILON = 2f;

        // Amount of positions with timestamp that older than timestamp of the last passed message, that will be skip in one frame.
        public const int BEHIND_EXTRAPOLATION_BATCH = 10;
        public const float ZERO_VELOCITY_SQR_THRESHOLD = 0.01f * 0.01f;

        // Found empirically and diverges a bit from the character settings (where speeds are RUN = 10, JOG = 8, WALK = 1.5)
        private const float RUN_SPEED_THRESHOLD = 9.5f;
        private const float JOG_SPEED_THRESHOLD = 4f;

        public static float GetBlendValueFromSpeed(float speed) =>
            speed > RUN_SPEED_THRESHOLD ? 3 : speed > JOG_SPEED_THRESHOLD ? 2 : 1;
    }
}
