namespace DCL.CharacterMotion.Components
{
    public struct JumpTrigger
    {
        public int TickWhenJumpOccurred;

        public readonly bool IsAvailable(int physicsTick) =>
            physicsTick == TickWhenJumpOccurred;
    }
}
