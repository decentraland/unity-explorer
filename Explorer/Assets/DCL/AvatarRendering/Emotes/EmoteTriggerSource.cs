namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     Identifies how an emote was triggered.
    /// </summary>
    public enum EmoteTriggerSource
    {
        /// <summary>Click on a wheel slot or press [0-9] while the emotes wheel is open.</summary>
        WHEEL_SLOT,

        /// <summary>Press B+[0-9] while the emotes wheel is closed.</summary>
        SHORTCUT,
    }
}
