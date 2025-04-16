namespace DCL.UI.SharedSpaceManager
{
    // TODO: This struct must be moved to one of the Chat modules, or a new one
    public struct ChatControllerShowParams
    {
        /// <summary>
        /// Indicates whether the chat panel should be unfolded. If it is False, no action will be performed.
        /// </summary>
        public readonly bool Unfold;

        /// <summary>
        /// Indicates whether the input box of the chat panel should gain the focus after showing.
        /// </summary>
        public readonly bool HasToFocusInputBox;

        /// <summary>
        /// Constructor with all fields.
        /// </summary>
        /// <param name="unfold">Indicates whether the chat panel should be unfolded. If it is False, no action will be performed.</param>
        /// <param name="hasToFocusInputBox">Indicates whether the input box of the chat panel should gain the focus after showing</param>
        public ChatControllerShowParams(bool unfold, bool hasToFocusInputBox = false)
        {
            Unfold = unfold;
            HasToFocusInputBox = hasToFocusInputBox;
        }
    }
}
