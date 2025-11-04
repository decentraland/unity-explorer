using DCL.Communities.CommunitiesDataProvider.DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text announcementContent = null!;
        [SerializeField] private TMP_Text authorName = null!;

        public void Configure(CommunityPost announcementInfo)
        {
            announcementContent.text = announcementInfo.content;
            authorName.text = announcementInfo.authorName;

            // TODO (Santi): Avoid to use ForceRebuildLayoutImmediate removing the content size fitter and calculating the height manually
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) transform);
        }
    }
}
