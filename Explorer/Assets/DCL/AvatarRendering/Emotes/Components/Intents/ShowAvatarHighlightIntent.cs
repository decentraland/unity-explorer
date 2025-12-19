namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     Every frame this intent is added, it increases the opacity of the outline of an avatar.
    /// </summary>
    public readonly struct ShowAvatarHighlightIntent
    {
        /// <summary>
        ///     Whether the user can interact with the avatar.
        /// </summary>
        public readonly bool CanInteract;

        public ShowAvatarHighlightIntent(bool canInteract)
        {
            CanInteract = canInteract;
        }
    }
}