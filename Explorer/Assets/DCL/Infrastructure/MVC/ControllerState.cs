namespace MVC
{
    public enum ControllerState
    {
        /// <summary>
        ///     View is hidden or not instantiated
        /// </summary>
        ViewHidden = 0,

        /// <summary>
        ///     View is not focused (blurred)
        /// </summary>
        ViewBlurred = 1,

        /// <summary>
        ///     View is in focus
        /// </summary>
        ViewFocused = 2,

        /// <summary>
        ///     View is being hidden.
        /// </summary>
        ViewHiding = 3,

        /// <summary>
        ///     View is becoming visible.
        /// </summary>
        ViewShowing = 4
    }
}
