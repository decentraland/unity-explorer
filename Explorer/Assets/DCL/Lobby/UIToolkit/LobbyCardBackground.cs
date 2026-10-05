using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Backgrounds of the card thumbnails and pictures, cropped by the stylesheets' cover rather than stretched.
    /// </summary>
    public static class LobbyCardBackground
    {
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
