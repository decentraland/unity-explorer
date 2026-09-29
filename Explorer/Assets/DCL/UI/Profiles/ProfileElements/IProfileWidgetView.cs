using DCL.Utilities;

namespace DCL.UI.ProfileElements
{
    /// <summary>
    ///     The widget of the current user a panel embeds in its top bar, whatever draws it: <see cref="ProfileWidgetView" /> for uGUI,
    ///     <see cref="ProfileWidgetElement" /> for UI Toolkit.
    /// </summary>
    public interface IProfileWidgetView
    {
        string Name { set; }

        /// <summary>
        ///     The wallet tag that stands in for a claimed name; empty when there is nothing to show.
        /// </summary>
        string Address { set; }

        /// <summary>
        ///     Draws the picture <paramref name="thumbnail" /> resolves to from now on.
        /// </summary>
        void BindThumbnail(IReactiveProperty<ProfileThumbnailViewModel> thumbnail);
    }
}
