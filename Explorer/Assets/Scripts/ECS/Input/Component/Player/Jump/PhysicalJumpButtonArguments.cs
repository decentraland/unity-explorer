namespace ECS.Input.Component
{
    public struct PhysicalJumpButtonArguments
    {
        public int tickWhenJumpOcurred;
        public float Power;

        public float GetPower(int physicsTick)
        {
            if (physicsTick == tickWhenJumpOcurred)
                return Power;

            return 0;
        }
    }
}
