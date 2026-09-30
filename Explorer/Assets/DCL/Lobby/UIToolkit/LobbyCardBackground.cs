using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Backgrounds of the card thumbnails and pictures. A full-rect sprite is handed over as its texture, which the cover of
    ///     the stylesheets crops to the element rather than stretching over it.
    /// </summary>
    public static class LobbyCardBackground
    {
        /// <summary>
        ///     Null clears the inline image, so the default one of the stylesheet shows. A sprite cut out of a bigger texture keeps
        ///     its own rect, since its texture would show the rest of the atlas.
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
