using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     A lobby card whose thumbnail arrives after the card is filled: the loading state shows while the sprite is on its way.
    /// </summary>
    public interface ILobbyThumbnailCard
    {
        /// <summary>
        ///     Null falls back to the default thumbnail of the card's stylesheet.
        /// </summary>
        Sprite? Thumbnail { set; }

        bool IsLoading { set; }
    }
}
