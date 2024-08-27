namespace DCL.SDKComponents.InputModifier.Components
{
    /// <summary>
    /// Allows systems to modify different aspects of the player input.
    /// </summary>
    public struct InputModifierComponent
    {
        private bool disableAll;
        private bool disableWalk;
        private bool disableJog;
        private bool disableRun;
        private bool disableJump;
        private bool disableEmote;

        /// <summary>
        /// When set to true, disables all related properties (Walk, Jog, Run, Jump, Emote).
        /// When set to false, only this property is affected.
        /// </summary>
        public bool DisableAll
        {
            get => disableAll;

            set
            {
                disableAll = value;
                disableWalk = value;
                disableJog = value;
                disableRun = value;
                disableJump = value;
                disableEmote = value;
            }
        }

        /// <summary>
        /// Gets or sets the DisableWalk property.
        /// <para>Get: Returns true if DisableAll is true or if DisableWalk is explicitly set to true.</para>
        /// <para>Set: Explicitly sets the DisableWalk property to the given value.</para>
        /// </summary>
        public bool DisableWalk { get => disableAll || disableWalk; set => disableWalk = value; }
        /// <summary>
        /// Gets or sets the DisableJog property.
        /// <para>Get: Returns true if DisableAll is true or if DisableJog is explicitly set to true.</para>
        /// <para>Set: Explicitly sets the DisableJog property to the given value.</para>
        /// </summary>
        public bool DisableJog{ get => disableAll || disableJog; set => disableJog = value; }
        /// <summary>
        /// Gets or sets the DisableRun property.
        /// <para>Get: Returns true if DisableAll is true or if DisableRun is explicitly set to true.</para>
        /// <para>Set: Explicitly sets the DisableRun property to the given value.</para>
        /// </summary>
        public bool DisableRun{ get => disableAll || disableRun; set => disableRun = value; }
        /// <summary>
        /// Gets or sets the DisableJump property.
        /// <para>Get: Returns true if DisableAll is true or if DisableJump is explicitly set to true.</para>
        /// <para>Set: Explicitly sets the DisableJump property to the given value.</para>
        /// </summary>
        public bool DisableJump{ get => disableAll || disableJump; set => disableJump = value; }
        /// <summary>
        /// Gets or sets the DisableEmote property.
        /// <para>Get: Returns true if DisableAll is true or if DisableEmote is explicitly set to true.</para>
        /// <para>Set: Explicitly sets the DisableEmote property to the given value.</para>
        /// </summary>
        public bool DisableEmote{ get => disableAll || disableEmote; set => disableEmote = value; }
    }
}
