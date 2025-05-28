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
        /// Constructor with all fields.
        /// </summary>
        /// <param name="unfold">Indicates whether the chat panel should be unfolded. If it is False, no action will be performed.</param>
        /// <param name="focus">Indicates whether the chat panel should gain the focus after showing.</param>
        public ChatControllerShowParams(bool unfold, bool focus = false)
        {
            Unfold = unfold;
            Focus = focus;
        }
    }
}
