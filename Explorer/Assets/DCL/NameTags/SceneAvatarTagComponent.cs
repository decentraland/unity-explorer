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
        ///     Equals <see cref="BackgroundColor" /> when the scene requested no border, leaving the plate flat.
        /// </summary>
        public readonly Color BorderColor;

        /// <summary>
        ///     The scene's raw max distance for this avatar's whole nametag; zero when the scene left it unset.
        /// </summary>
        public readonly float MaxDistance;

        public bool IsDirty;
        public bool IsRemoving;

        public SceneAvatarTagComponent(string text, Color textColor, Color backgroundColor, Color borderColor, float maxDistance)
        {
            Text = text;
            TextColor = textColor;
            BackgroundColor = backgroundColor;
            BorderColor = borderColor;
            MaxDistance = maxDistance;

            IsDirty = true;
            IsRemoving = false;
        }
    }
}
