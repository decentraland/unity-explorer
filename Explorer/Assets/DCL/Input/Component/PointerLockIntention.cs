namespace DCL.Input.Component
{
    public struct PointerLockIntention
    {
        public readonly bool Locked;

        /// <summary>
        ///     Whether an interactable menu was spawned while locked.
        /// </summary>
        public readonly bool WithUI;

        /// <summary>
        ///     Default constructor.
        /// </summary>
        /// <param name="locked">Whether the cursor will be Locked.</param>
        /// <param name="withUI">Whether an interactable menu was spawned while locked.</param>
        public PointerLockIntention(bool locked, bool withUI = false)
        {
            Locked = locked;
            WithUI = withUI;
        }
    }
}
