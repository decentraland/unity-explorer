
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    /// Every frame this intent is added, it increases the opacity of the outline of an avatar.
    /// </summary>
    public readonly struct ShowAvatarHighlightIntent
    {
        public readonly float Thickness;
        public readonly Color OutlineColor;

        public ShowAvatarHighlightIntent(float thickness, Color outlineColor)
        {
            Thickness = thickness;
            OutlineColor = outlineColor;
        }
    }
}
