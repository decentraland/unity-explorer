using System;

namespace DCL.UI.Credits
{
    /// <summary>
    ///     The credits widget a fullscreen panel embeds in its top bar: <see cref="CreditsPanelView" /> for uGUI, <see cref="CreditsPanelElement" /> for UI Toolkit.
    /// </summary>
    public interface ICreditsPanelView
    {
        bool IsShown { set; }

        /// <summary>
        ///     Whether the widget offers a way to get more credits, reported through <see cref="GetCreditsClicked" />.
        /// </summary>
        bool IsTopUpEnabled { set; }

        string Credits { set; }

        Action? GetCreditsClicked { get; set; }
    }
}
