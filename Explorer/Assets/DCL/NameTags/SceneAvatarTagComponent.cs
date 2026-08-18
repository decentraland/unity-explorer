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
        public readonly string Text;
        public readonly Color TextColor;
        public readonly Color BackgroundColor;

        public bool IsDirty;
        public bool IsRemoving;

        public SceneAvatarTagComponent(string text, Color textColor, Color backgroundColor)
        {
            Text = text;
            TextColor = textColor;
            BackgroundColor = backgroundColor;
            IsDirty = true;
            IsRemoving = false;
        }
    }
}
