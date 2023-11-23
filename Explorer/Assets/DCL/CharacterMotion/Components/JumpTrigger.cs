namespace DCL.CharacterMotion.Components
{
    public struct JumpTrigger
    {
        public int TickWhenJumpOccurred;

        public readonly bool IsAvailable(int physicsTick, int bonusFrames) =>
            physicsTick == TickWhenJumpOccurred || physicsTick <= TickWhenJumpOccurred + bonusFrames;

        public void Reset() =>
            TickWhenJumpOccurred = -999;
    }
}
