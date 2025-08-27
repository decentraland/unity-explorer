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
        private Tweener? descriptionTween;
        private const float HOVER_ANIMATION_DURATION = 0.3f;
        private const float HOVER_ANIMATION_HEIGHT_TO_APPLY = 45f;

        public event Action<string>? MainButtonClicked;

        [SerializeField] private RectTransform headerContainer = null!;
        [SerializeField] private RectTransform footerContainer = null!;
        [SerializeField] private TMP_Text communityTitle = null!;
        [field: SerializeField] public ImageView communityThumbnail = null!;
        [SerializeField] private Button mainButton = null!;
        [SerializeField] private ListenersCountView listenersCountView;

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

            originalHeaderSizeDelta = headerContainer.sizeDelta;
            originalFooterSizeDelta = footerContainer.sizeDelta;
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
            headerTween?.Kill();
            footerTween?.Kill();
            descriptionTween?.Kill();

            headerTween = DOTween.To(() =>
                          headerContainer.sizeDelta,
                          newSizeDelta => headerContainer.sizeDelta = newSizeDelta,
                          new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y - HOVER_ANIMATION_HEIGHT_TO_APPLY),
                          HOVER_ANIMATION_DURATION)
                     .SetEase(Ease.OutQuad);

            footerTween = DOTween.To(() =>
                          footerContainer.sizeDelta,
                          newSizeDelta => footerContainer.sizeDelta = newSizeDelta,
                          new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y + HOVER_ANIMATION_HEIGHT_TO_APPLY),
                          HOVER_ANIMATION_DURATION)
                     .SetEase(Ease.OutQuad);
        }

        private void PlayHoverExitAnimation(bool instant = false)
        {
            headerTween?.Kill();
            footerTween?.Kill();
            descriptionTween?.Kill();

            if (instant)
            {
                headerContainer.sizeDelta = originalHeaderSizeDelta;
                footerContainer.sizeDelta = originalFooterSizeDelta;
            }
            else
            {
                headerTween = DOTween.To(() =>
                              headerContainer.sizeDelta,
                              x => headerContainer.sizeDelta = x,
                              new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y),
                              HOVER_ANIMATION_DURATION)
                         .SetEase(Ease.OutQuad);

                footerTween = DOTween.To(() =>
                              footerContainer.sizeDelta,
                              x => footerContainer.sizeDelta = x,
                              new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y),
                              HOVER_ANIMATION_DURATION)
                         .SetEase(Ease.OutQuad);

            }
        }
    }
}
