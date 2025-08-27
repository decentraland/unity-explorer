using DCL.UI;
using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DCL.Communities.CommunitiesBrowser
{
    public class StreamingCommunityResultCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<string>? MainButtonClicked;

        [SerializeField] private RectTransform hoverOverlay = null!;
        [SerializeField] private TMP_Text communityTitle = null!;
        [field: SerializeField] public ImageView communityThumbnail = null!;
        [SerializeField] private Button mainButton = null!;
        [SerializeField] private ListenersCountView listenersCountView = null!;

        private readonly StringBuilder stringBuilder = new ();

        [Serializable]
        internal struct MutualFriendsConfig
        {
            public MutualThumbnail[] thumbnails;

            [Serializable]
            public struct MutualThumbnail
            {
                public GameObject root;
                public ProfilePictureView picture;
                public ProfileNameTooltipView profileNameTooltip;

                internal bool isPointerEventsSubscribed;
            }
        }

        private string? currentCommunityId;
        private Tweener? headerTween;
        private Tweener? footerTween;
        private Vector2 originalHeaderSizeDelta;
        private Vector2 originalFooterSizeDelta;

        private void Awake()
        {
            mainButton.onClick.AddListener(() =>
            {
                if (currentCommunityId != null)
                    MainButtonClicked?.Invoke(currentCommunityId);
            });
        }

        private void OnEnable() =>
            PlayHoverExitAnimation(instant: true);

        private void OnDestroy()
        {
            mainButton.onClick.RemoveAllListeners();
        }

        public void SetCommunityId(string id) =>
            currentCommunityId = id;

        public void SetTitle(string title) =>
            communityTitle.text = title;

        public void ConfigureListenersCount(bool isActive, int listenersCount)
        {
            listenersCountView.gameObject.SetActive(isActive);
            stringBuilder.Clear();
            stringBuilder.Append(listenersCount);
            listenersCountView.ParticipantCount.text = stringBuilder.ToString();
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            PlayHoverAnimation();

        public void OnPointerExit(PointerEventData eventData) =>
            PlayHoverExitAnimation();

        private void PlayHoverAnimation()
        {
            hoverOverlay.gameObject.SetActive(true);
        }

        private void PlayHoverExitAnimation(bool instant = false)
        {
            hoverOverlay.gameObject.SetActive(false);
        }
    }
}
