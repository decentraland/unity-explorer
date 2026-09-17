using DCL.Communities;
using DCL.PlacesAPIService;
using DCL.UI;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     Compact place card of the lobby: thumbnail, title and creator. Clicking it jumps into the place.
    /// </summary>
    public class LobbyPlaceCardView : MonoBehaviour
    {
        [field: SerializeField]
        public Button Button { get; private set; } = null!;

        [field: SerializeField]
        public ImageView Thumbnail { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text TitleText { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text CreatorText { get; private set; } = null!;

        [SerializeField] private Sprite? defaultThumbnail;

        public PlacesData.PlaceInfo? Place { get; private set; }

        public void Show(PlacesData.PlaceInfo place, ThumbnailLoader thumbnailLoader, CancellationToken ct)
        {
            Place = place;
            TitleText.text = place.title;
            CreatorText.text = place.contact_name;
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(place.image, Thumbnail, defaultThumbnail, ct, true).Forget();

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            Place = null;
            gameObject.SetActive(false);
        }
    }
}
