using TMPro;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Compact place card of the lobby: thumbnail, title and creator.
    /// </summary>
    public class LobbyPlaceCardView : LobbyCardView
    {
        [field: SerializeField]
        public TMP_Text CreatorText { get; private set; } = null!;
    }
}
