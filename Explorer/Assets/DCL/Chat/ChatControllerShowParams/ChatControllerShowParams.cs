namespace DCL.Chat.ControllerShowParams
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

        public readonly bool ShowLastState;

        /// <summary>
        /// Constructor with all fields.
        /// </summary>
        /// <param name="showUnfolded">Indicates whether the chat panel should be folded or unfolded when its view is shown.</param>
        /// <param name="showLastState">Indicates whether the chat panel should restore its previous state when shown again.</param>
        /// <param name="hasToFocusInputBox">Indicates whether the input box of the chat panel should gain the focus after showing.</param>
        public ChatControllerShowParams(bool showUnfolded, bool showLastState = false, bool hasToFocusInputBox = false)
        {
            ShowUnfolded = showUnfolded;
            ShowLastState = showLastState;
            HasToFocusInputBox = hasToFocusInputBox;
        }
    }
}
