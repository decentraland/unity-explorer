﻿namespace MVC
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
    }
}
