using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Backgrounds of the card thumbnails and pictures, cropped by the stylesheets' cover rather than stretched.
    /// </summary>
    public static class LobbyCardBackground
    {
        // Opacity of the white frame the uGUI ProfilePictureView lays over the profile color
        private const float FRAME_WHITE_BLEND = 0.2f;

        /// <summary>
        ///     The border is not backed by the background, so its lighter shade is computed rather than left translucent.
        /// </summary>
        public static void ApplyPictureColor(VisualElement picture, Color profileColor)
        {
            Color frame = Color.Lerp(profileColor, Color.white, FRAME_WHITE_BLEND);
            picture.style.backgroundColor = profileColor;
            picture.style.borderTopColor = frame;
            picture.style.borderRightColor = frame;
            picture.style.borderBottomColor = frame;
            picture.style.borderLeftColor = frame;
        }

        /// <summary>
        ///     Null clears the inline image; a full-rect sprite is set as its texture, an atlas sprite keeps its rect.
        /// </summary>
        public static StyleBackground From(Sprite? sprite)
        {
            if (sprite == null)
                return StyleKeyword.Null;

            Texture2D texture = sprite.texture;
            Rect rect = sprite.rect;

            bool coversTexture = rect.x == 0 && rect.y == 0 && Mathf.Approximately(rect.width, texture.width) && Mathf.Approximately(rect.height, texture.height);
            return coversTexture ? new StyleBackground(texture) : new StyleBackground(sprite);
        }
    }
}
