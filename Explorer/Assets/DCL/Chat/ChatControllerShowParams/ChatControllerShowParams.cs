namespace DCL.Chat.ControllerShowParams
{
    // TODO: This struct must be moved to one of the Chat modules, or a new one
    public struct ChatControllerShowParams
    {
        /// <summary>
        /// Indicates whether the chat panel should be unfolded. If it is False, no action will be performed.
        /// </summary>
        public readonly bool Unfold;

        /// <summary>
        /// Indicates whether the chat panel should gain the focus after showing.
        /// </summary>
        public readonly bool Focus;

        /// <summary>
        ///     If true, the caller is a keyboard shortcut that must force focus (never toggle to minimized).
        /// </summary>
        public readonly bool ForceFocusFromShortcut;

        /// <summary>
        /// Constructor with all fields.
        /// </summary>
        /// <param name="unfold">Indicates whether the chat panel should be unfolded. If it is False, no action will be performed.</param>
        /// <param name="focus">Indicates whether the chat panel should gain the focus after showing.</param>
        public ChatControllerShowParams(bool unfold, bool focus = false, bool forceFocusFromShortcut = false)
        {
            Unfold = unfold;
            Focus = focus;
            ForceFocusFromShortcut = forceFocusFromShortcut;
        }
    }
}
