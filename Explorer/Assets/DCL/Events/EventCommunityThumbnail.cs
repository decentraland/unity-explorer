using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Events
{
    public class EventCommunityThumbnail : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private ImageView communityThumbnail = null!;
        [SerializeField] private GameObject communityTooltip = null!;
        [SerializeField] private TMP_Text communityText = null!;

        private void OnEnable() =>
            communityTooltip.SetActive(false);

        public void Configure(GetUserCommunitiesData.CommunityData communityInfo, ThumbnailLoader thumbnailLoader, CancellationToken ct)
        {
            thumbnailLoader.LoadCommunityThumbnailFromUrlAsync(communityInfo.thumbnailUrl, communityThumbnail, null, ct, true).Forget();
            communityText.text = communityInfo.name;
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            communityTooltip.SetActive(true);

        public void OnPointerExit(PointerEventData eventData) =>
            communityTooltip.SetActive(false);
    }
}
