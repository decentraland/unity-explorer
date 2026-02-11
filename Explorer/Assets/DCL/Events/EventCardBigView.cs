using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Events
{
    public class EventCardBigView : EventCardView
    {
        private const float HOVER_ANIMATION_DURATION = 0.3f;
        private const float HOVER_ANIMATION_HEIGHT_TO_APPLY = 64f;

        [Header("Specific-card references")]
        [SerializeField] private EventCommunityThumbnail eventCommunityThumbnail = null!;

        [Header("Animations")]
        [SerializeField] private RectTransform headerContainer = null!;
        [SerializeField] private RectTransform footerContainer = null!;
        [SerializeField] private CanvasGroup actionButtonsCanvasGroup = null!;
        [SerializeField] private CanvasGroup reminderMarkCanvasGroup = null!;
        [SerializeField] private CanvasGroup liveMarkCanvasGroup = null!;

        private Tweener? headerContainerTween;
        private Tweener? footerContainerTween;
        private Tweener? actionButtonsCanvasGroupTween;
        private Tweener? reminderMarkCanvasGroupTween;
        private Tweener? liveMarkCanvasGroupTween;
        private Vector2 originalHeaderSizeDelta;
        private Vector2 originalFooterSizeDelta;

        private CancellationTokenSource? loadingCommunityThumbnailCts;

        protected override void Awake()
        {
            base.Awake();
            originalHeaderSizeDelta = headerContainer.sizeDelta;
            originalFooterSizeDelta = footerContainer.sizeDelta;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            loadingCommunityThumbnailCts.SafeCancelAndDispose();
        }

        public override void Configure(EventDTO eventInfo, ThumbnailLoader thumbnailLoader, PlacesData.PlaceInfo? placeInfo = null,
            List<Profile.CompactInfo>? friends = null, ProfileRepositoryWrapper? profileRepositoryWrapper = null, GetUserCommunitiesData.CommunityData? communityInfo = null)
        {
            base.Configure(eventInfo, thumbnailLoader, placeInfo, friends, profileRepositoryWrapper, communityInfo);

            eventCommunityThumbnail.gameObject.SetActive(communityInfo != null);

            if (communityInfo != null)
            {
                loadingCommunityThumbnailCts = loadingCommunityThumbnailCts.SafeRestart();
                eventCommunityThumbnail.Configure(communityInfo, thumbnailLoader, loadingCommunityThumbnailCts.Token);
            }
        }

        protected override void PlayHoverAnimation()
        {
            headerContainerTween?.Kill();
            footerContainerTween?.Kill();
            actionButtonsCanvasGroupTween?.Kill();
            reminderMarkCanvasGroupTween?.Kill();
            liveMarkCanvasGroupTween?.Kill();

            headerContainerTween = DOTween.To(() =>
                                          headerContainer.sizeDelta,
                                      newSizeDelta => headerContainer.sizeDelta = newSizeDelta,
                                      new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y - HOVER_ANIMATION_HEIGHT_TO_APPLY),
                                      HOVER_ANIMATION_DURATION)
                                 .SetEase(Ease.OutQuad);

            footerContainerTween = DOTween.To(() =>
                                          footerContainer.sizeDelta,
                                      newSizeDelta => footerContainer.sizeDelta = newSizeDelta,
                                      new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y + HOVER_ANIMATION_HEIGHT_TO_APPLY),
                                      HOVER_ANIMATION_DURATION)
                                 .SetEase(Ease.OutQuad);

            actionButtonsCanvasGroupTween = DOTween.To(() =>
                                               actionButtonsCanvasGroup.alpha,
                                           newAlpha => actionButtonsCanvasGroup.alpha = newAlpha,
                                           1f,
                                           HOVER_ANIMATION_DURATION)
                                      .SetEase(Ease.OutQuad);

            reminderMarkCanvasGroupTween = DOTween.To(() =>
                                                            reminderMarkCanvasGroup.alpha,
                                                        newAlpha => reminderMarkCanvasGroup.alpha = newAlpha,
                                                        0f,
                                                        HOVER_ANIMATION_DURATION)
                                                   .SetEase(Ease.OutQuad);

            liveMarkCanvasGroupTween = DOTween.To(() =>
                                                           liveMarkCanvasGroup.alpha,
                                                       newAlpha => liveMarkCanvasGroup.alpha = newAlpha,
                                                       0f,
                                                       HOVER_ANIMATION_DURATION)
                                                  .SetEase(Ease.OutQuad);
        }

        protected override void PlayHoverExitAnimation(bool instant = false)
        {
            headerContainerTween?.Kill();
            footerContainerTween?.Kill();
            actionButtonsCanvasGroupTween?.Kill();
            reminderMarkCanvasGroupTween?.Kill();
            liveMarkCanvasGroupTween?.Kill();

            if (instant)
            {
                headerContainer.sizeDelta = originalHeaderSizeDelta;
                footerContainer.sizeDelta = originalFooterSizeDelta;
                actionButtonsCanvasGroup.alpha = 0f;
            }
            else
            {
                headerContainerTween = DOTween.To(() =>
                                              headerContainer.sizeDelta,
                                          x => headerContainer.sizeDelta = x,
                                          new Vector2(headerContainer.sizeDelta.x, originalHeaderSizeDelta.y),
                                          HOVER_ANIMATION_DURATION)
                                     .SetEase(Ease.OutQuad);

                footerContainerTween = DOTween.To(() =>
                                              footerContainer.sizeDelta,
                                          x => footerContainer.sizeDelta = x,
                                          new Vector2(footerContainer.sizeDelta.x, originalFooterSizeDelta.y),
                                          HOVER_ANIMATION_DURATION)
                                     .SetEase(Ease.OutQuad);

                actionButtonsCanvasGroupTween = DOTween.To(() =>
                                                   actionButtonsCanvasGroup.alpha,
                                               newAlpha => actionButtonsCanvasGroup.alpha = newAlpha,
                                               0f,
                                               HOVER_ANIMATION_DURATION)
                                          .SetEase(Ease.OutQuad);

                reminderMarkCanvasGroupTween = DOTween.To(() =>
                                                               reminderMarkCanvasGroup.alpha,
                                                           newAlpha => reminderMarkCanvasGroup.alpha = newAlpha,
                                                           1f,
                                                           HOVER_ANIMATION_DURATION)
                                                      .SetEase(Ease.OutQuad);

                liveMarkCanvasGroupTween = DOTween.To(() =>
                                                           liveMarkCanvasGroup.alpha,
                                                       newAlpha => liveMarkCanvasGroup.alpha = newAlpha,
                                                       1f,
                                                       HOVER_ANIMATION_DURATION)
                                                  .SetEase(Ease.OutQuad);
            }
        }
    }
}
