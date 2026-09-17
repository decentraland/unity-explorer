using DCL.Communities;
using DCL.PlacesAPIService;
using DCL.UI;
using System.Threading;
using TMPro;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     Hero card of the lobby: the place the user calls home (or Genesis Plaza) with its thumbnail, title, creator,
    ///     online users and a Jump in button. Jump in stays disabled until a place is shown.
    /// </summary>
    public class LobbyHomeCardView : MonoBehaviour
    {
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

        public PlacesData.PlaceInfo? Place { get; private set; }

        public void ShowLoading()
        {
            Place = null;
            TitleText.text = string.Empty;
            CreatorText.text = string.Empty;
            OnlineCounter.SetActive(false);
            Thumbnail.IsLoading = true;
            JumpInButton.SetInteractable(false);
        }

        public void Show(PlacesData.PlaceInfo place, ThumbnailLoader thumbnailLoader, CancellationToken ct)
        {
            Place = place;
            TitleText.text = place.title;
            CreatorText.text = place.contact_name;

            int online = place.connected_addresses?.Length ?? place.user_count;
            OnlineCountText.text = online.ToString();
            OnlineCounter.SetActive(online > 0);

            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(place.image, Thumbnail, defaultThumbnail, ct, true).Forget();
            JumpInButton.SetInteractable(true);
        }
    }
}
