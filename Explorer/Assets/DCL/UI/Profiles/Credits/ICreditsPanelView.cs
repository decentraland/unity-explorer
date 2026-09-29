using System;

namespace DCL.UI.Credits
{
    /// <summary>
    ///     The credits widget a fullscreen panel embeds in its top bar, whatever draws it: <see cref="CreditsPanelView" /> for uGUI,
    ///     <see cref="CreditsPanelElement" /> for UI Toolkit.
    /// </summary>
    public interface ICreditsPanelView
    {
        /// <summary>
        ///     Off until the credits feature is confirmed for the current user.
        /// </summary>
        bool IsShown { set; }

        /// <summary>
        ///     Whether the widget offers a way to get more credits, reported through <see cref="GetCreditsClicked" />.
        /// </summary>
        bool IsTopUpEnabled { set; }

        string Credits { set; }

        Action? GetCreditsClicked { get; set; }
    }
}
