namespace DCL.Input.Component
{
    public struct PointerLockIntention
    {
        public readonly bool Locked;
        public readonly bool WithUI;

        public PointerLockIntention(bool locked, bool withUI = false)
        {
            Locked = locked;
            WithUI = withUI;
        }
    }
}
