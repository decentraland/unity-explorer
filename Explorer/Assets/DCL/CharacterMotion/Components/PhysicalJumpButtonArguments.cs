namespace DCL.CharacterMotion.Components
{
    public struct PhysicalJumpButtonArguments
    {
        public int TickWhenJumpOccurred;
        public float Power;

        public float GetPower(int physicsTick)
        {
            if (physicsTick == TickWhenJumpOccurred)
                return Power;

            return 0;
        }
    }
}
