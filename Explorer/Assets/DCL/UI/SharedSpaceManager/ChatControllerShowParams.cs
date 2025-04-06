namespace DCL.UI.SharedSpaceManager
{
    public struct ChatControllerShowParams
    {
        /// <summary>
        /// Indicates whether the chat panel should be folded or unfolded when its view is shown.
        /// </summary>
        public readonly bool ShowUnfolded;

        /// <summary>
        /// Indicates whether the input box of the chat panel should gain the focus after showing.
        /// </summary>
        public readonly bool HasToFocusInputBox;

        /// <summary>
        /// Constructor with all fields.
        /// </summary>
        /// <param name="showUnfolded">Indicates whether the chat panel should be folded or unfolded when its view is shown.</param>
        /// <param name="hasToFocusInputBox">Indicates whether the input box of the chat panel should gain the focus after showing</param>
        public ChatControllerShowParams(bool showUnfolded, bool hasToFocusInputBox = false)
        {
            ShowUnfolded = showUnfolded;
            HasToFocusInputBox = hasToFocusInputBox;
        }
    }
}
