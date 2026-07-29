namespace DCL.CharacterMotion.Components
{
    public struct JumpState
    {
        /// <summary>
        ///     Last tick during which the avatar was grounded.
        /// </summary>
        public int LastGroundedTick;

        /// <summary>
        ///     Whether the avatar started a jump during this tick.
        ///     Only available after ApplyJump logic has been executed.
        /// </summary>
        public bool JustJumped;

        /// <summary>
        ///     How many times the avatar has jumped.
        ///     It's reset when touching the ground again.
        /// </summary>
        public int JumpCount;

        /// <summary>
        ///     Maximum number of air jumps the avatar can perform.
        /// </summary>
        public int MaxAirJumpCount;

        /// <summary>
        ///     When positive indicates that we are awaiting the air jump delay period.
        ///     When the timer expires, the air jump is actually performed.
        /// </summary>
        public float AirJumpDelay;

        /// <summary>
        ///     Last tick at which a mid-air descending upward impulse reset the jump counter.
        ///     Used to throttle that reset so rapidly repeated impulses don't grant jumps every tick.
        /// </summary>
        public int LastDescendingResetTick;

        public readonly bool IsCoyoteTimeActive(int currentTick, int coyoteTimeTickCount) =>
            currentTick - LastGroundedTick < coyoteTimeTickCount;
    }
}
