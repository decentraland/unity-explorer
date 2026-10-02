using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Backgrounds of the card thumbnails and pictures, cropped by the cover of the stylesheets rather than stretched.
    /// </summary>
    public static class LobbyCardBackground
    {
        /// <summary>
        ///     Null clears the inline image; a full-rect sprite is handed over as its texture, one cut out of an atlas keeps its own rect.
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
