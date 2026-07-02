namespace DCL.CharacterMotion.Components
{
    // Number defines movement blend id in animator
    public enum MovementKind : byte
    {
        IDLE = 0,
        WALK = 1,
        JOG = 2,
        RUN = 3,
    }

    public static class MovementBlend
    {
        public const byte MIN = (byte)MovementKind.IDLE;
        public const byte MAX = (byte)MovementKind.RUN;
    }
}
