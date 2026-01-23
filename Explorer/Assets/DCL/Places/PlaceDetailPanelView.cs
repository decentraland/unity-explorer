using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.PlacesAPIService;
using DCL.UI;
using MVC;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Places
{
    public class PlaceDetailPanelView : ViewBase, IView
    {
        [SerializeField] private Button backgroundCloseButton = null!;
        [SerializeField] private Button closeButton = null!;
        [SerializeField] private Sprite defaultPlaceThumbnail = null!;

        [SerializeField] private ImageView placeThumbnailImage = null!;
        [SerializeField] private TMP_Text placeNameText = null!;

        private readonly UniTask[] closeTasks = new UniTask[2];

        private CancellationToken ct;
        private CancellationTokenSource loadingThumbnailCts;

        public UniTask[] GetCloseTasks()
        {
            closeTasks[0] = backgroundCloseButton.OnClickAsync(ct);
            closeTasks[1] = closeButton.OnClickAsync(ct);
            return closeTasks;
        }

        public void ConfigurePlaceData(PlacesData.PlaceInfo placeInfo, ThumbnailLoader thumbnailLoader, CancellationToken cancellationToken)
        {
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(placeInfo.image, placeThumbnailImage, defaultPlaceThumbnail, cancellationToken, true).Forget();
            placeNameText.text = placeInfo.title;
        }
    }
}
