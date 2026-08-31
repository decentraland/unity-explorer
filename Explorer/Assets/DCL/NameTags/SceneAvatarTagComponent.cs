using UnityEngine;

namespace DCL.Nametags
{
    /// <summary>
    ///     Scene-driven tag displayed on its own plate above the avatar nametag.
    ///     Presence of this component (with <see cref="IsRemoving" /> false) keeps the nametag holder alive
    ///     even when the native name is hidden. To remove the tag, set <see cref="IsRemoving" /> instead of
    ///     removing the component directly, so the plate is hidden before the component goes away.
    /// </summary>
    public struct SceneAvatarTagComponent
    {
        /// <summary>
        ///     Mirrors the plate label's native color, `--dcl-color-snow` (rgb(252, 252, 252)) in CommonStyles.uss.
        /// </summary>
        public static readonly Color NATIVE_TEXT_COLOR = new (252f / 255f, 252f / 255f, 252f / 255f);

        /// <summary>
        ///     Mirrors the plate's native background, `--dcl-color-shadow` (#161518) in CommonStyles.uss.
        /// </summary>
        public static readonly Color NATIVE_BACKGROUND_COLOR = new (22f / 255f, 21f / 255f, 24f / 255f);

        public readonly string Text;
        public readonly Color TextColor;
        public readonly Color BackgroundColor;

        /// <summary>
        ///     The rim of the plate. Holds <see cref="BackgroundColor" /> unless a border was explicitly
        ///     requested; the rim always renders at its USS width, so a flat plate keeps the same size.
        /// </summary>
        public readonly Color BorderColor;

        public bool IsDirty;
        public bool IsRemoving;

        public SceneAvatarTagComponent(string text, Color textColor, Color backgroundColor, Color borderColor)
        {
            Text = text;
            TextColor = textColor;
            BackgroundColor = backgroundColor;
            BorderColor = borderColor;
            IsDirty = true;
            IsRemoving = false;
        }
    }
}
