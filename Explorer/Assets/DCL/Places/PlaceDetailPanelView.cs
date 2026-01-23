using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Places
{
    public class PlaceDetailPanelView : ViewBase, IView
    {
        [Header("Panel")]
        [SerializeField] private Button backgroundCloseButton = null!;
        [SerializeField] private Button closeButton = null!;

        [Header("Place info")]
        [SerializeField] private ImageView placeThumbnailImage = null!;
        [SerializeField] private Sprite defaultPlaceThumbnail = null!;
        [SerializeField] private TMP_Text placeNameText = null!;
        [SerializeField] private ProfilePictureView creatorThumbnail = null!;
        [SerializeField] private TMP_Text creatorNameText = null!;
        [SerializeField] private TMP_Text likeRateText = null!;
        [SerializeField] private TMP_Text visitsText = null!;

        private readonly UniTask[] closeTasks = new UniTask[2];

        private CancellationToken ct;

        public UniTask[] GetCloseTasks()
        {
            closeTasks[0] = backgroundCloseButton.OnClickAsync(ct);
            closeTasks[1] = closeButton.OnClickAsync(ct);
            return closeTasks;
        }

        public void ConfigurePlaceData(
            PlacesData.PlaceInfo placeInfo,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            Profile.CompactInfo? creatorProfile,
            CancellationToken cancellationToken)
        {
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(placeInfo.image, placeThumbnailImage, defaultPlaceThumbnail, cancellationToken, true).Forget();
            placeNameText.text = placeInfo.title;

            creatorThumbnail.gameObject.SetActive(creatorProfile != null);
            if (creatorProfile != null)
                creatorThumbnail.Setup(profileRepositoryWrapper, creatorProfile.Value);

            creatorNameText.text = placeInfo.contact_name;
            likeRateText.text = $"{(placeInfo.like_rate_as_float ?? 0) * 100:F0}%";
            visitsText.text = GetKFormat(placeInfo.user_visits);
        }

        private static string GetKFormat(int num)
        {
            if (num < 1000)
                return num.ToString();

            float divided = num / 1000.0f;
            divided = (int)(divided * 100) / 100f;
            return $"{divided:F2}k";
        }
    }
}
