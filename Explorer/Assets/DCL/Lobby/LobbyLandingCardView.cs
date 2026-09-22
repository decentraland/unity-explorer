using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     Hero card of the lobby: the place the session lands in, with its thumbnail, title, creator,
    ///     online users, a Jump in button and a button covering the whole card.
    /// </summary>
    public class LobbyLandingCardView : MonoBehaviour
    {
        [field: SerializeField]
        public Button Button { get; private set; } = null!;

        [field: SerializeField]
        public ButtonView JumpInButton { get; private set; } = null!;

        [field: SerializeField]
        public ImageView Thumbnail { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text TitleText { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text CreatorText { get; private set; } = null!;

        [field: SerializeField]
        public GameObject OnlineCounter { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text OnlineCountText { get; private set; } = null!;

        [SerializeField] private Sprite? defaultThumbnail;

        public Sprite? DefaultThumbnail => defaultThumbnail;
    }
}
