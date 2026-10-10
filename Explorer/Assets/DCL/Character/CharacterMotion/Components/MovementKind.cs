namespace DCL.CharacterMotion.Components
{
    // Number defines movement blend id in animator
    public enum MovementKind : byte
    {
        Idle = 0,
        Walk = 1,
        Jog = 2,
        Run = 3,
    }

    public static class MovementBlend
    {
        public const byte MIN = (byte)MovementKind.Idle;
        public const byte MAX = (byte)MovementKind.Run;
    }
}
