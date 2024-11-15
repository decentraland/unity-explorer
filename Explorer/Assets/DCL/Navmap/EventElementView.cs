using DCL.UI;
using DCL.WebRequests;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class EventElementView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private ImageView thumbnailView;

        [SerializeField]
        private MultiStateButtonView interestedButtonView;

        [SerializeField]
        private GameObject highlightContainer;

        [field: SerializeField]
        public TMP_Text EventNameLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text InterestedUserCountLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text JoinedUserCountLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text ScheduleLabel { get; private set; }

        [field: SerializeField]
        public Button ShareButton { get; private set; }

        [field: SerializeField]
        public RectTransform SharePivot { get; private set; }

        [field: SerializeField]
        public Button ShowDetailsButton { get; private set; }

        [field: SerializeField]
        public GameObject LiveContainer { get; private set; }

        [field: SerializeField]
        public Animator Animator { get; private set; }

        public ImageController? Thumbnail { get; private set; }

        public MultiStateButtonController? InterestedButton { get; private set; }

        public void Init(IWebRequestController webRequestController)
        {
            Thumbnail = new ImageController(thumbnailView, webRequestController);
            InterestedButton = new MultiStateButtonController(interestedButtonView, true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            highlightContainer.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            highlightContainer.gameObject.SetActive(false);
        }
    }
}
