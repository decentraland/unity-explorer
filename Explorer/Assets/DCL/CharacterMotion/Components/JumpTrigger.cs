namespace DCL.CharacterMotion.Components
{
    public struct JumpTrigger
    {
        public int TickWhenJumpOccurred;
        public int TickWhenJumpWasConsumed;

        public readonly bool IsAvailable(int physicsTick, int bonusFrames) =>
            physicsTick == TickWhenJumpOccurred || physicsTick <= TickWhenJumpOccurred + bonusFrames;
    }
}
