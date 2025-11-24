using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementCardView : MonoBehaviour
    {
        private const string DELETE_ANNOUNCEMENT_TEXT_FORMAT = "Are you sure you want to delete this Announcement?";
        private const string DELETE_ANNOUNCEMENT_CONFIRM_TEXT = "DELETE";
        private const string DELETE_ANNOUNCEMENT_CANCEL_TEXT = "CANCEL";
        private const float MIN_CARD_HEIGHT = 87f;

        [SerializeField] private TMP_Text announcementContent = null!;
        [SerializeField] private TMP_Text authorName = null!;
        [SerializeField] private TMP_Text profileTag = null!;
        [SerializeField] private GameObject verifiedMark = null!;
        [SerializeField] private GameObject officialMark = null!;
        [SerializeField] private TMP_Text postDate = null!;
        [SerializeField] private Button likeAnnouncementButton = null!;
        [SerializeField] private Button unlikeAnnouncementButton = null!;
        [SerializeField] private TMP_Text likesCounter = null!;
        [SerializeField] private Button deleteAnnouncementButton = null!;
        [SerializeField] private ProfilePictureView profilePicture = null!;
        [SerializeField] private Sprite deleteSprite = null!;
        [SerializeField] private ContentSizeFitter messageContentSizeFitter = null!;

        private string currentAnnouncementId = null!;
        private string currentProfileThumbnailUrl = null!;

        private CancellationTokenSource confirmationDialogCts = null!;

        public event Action<string>? LikeAnnouncementButtonClicked;
        public event Action<string>? UnlikeAnnouncementButtonClicked;
        public event Action<string>? DeleteAnnouncementButtonClicked;

        private void Awake()
        {
            likeAnnouncementButton.onClick.AddListener(OnLikeAnnouncementButtonClicked);
            unlikeAnnouncementButton.onClick.AddListener(OnUnlikeAnnouncementButtonClicked);
            deleteAnnouncementButton.onClick.AddListener(OnDeleteAnnouncementButtonClicked);
        }

        private void OnDisable() =>
            confirmationDialogCts.SafeCancelAndDispose();

        private void OnDestroy()
        {
            likeAnnouncementButton.onClick.RemoveListener(OnLikeAnnouncementButtonClicked);
            unlikeAnnouncementButton.onClick.RemoveListener(OnUnlikeAnnouncementButtonClicked);
            deleteAnnouncementButton.onClick.RemoveListener(OnDeleteAnnouncementButtonClicked);
        }

        public void Configure(CommunityPost announcementInfo, ProfileRepositoryWrapper profileDataProvider, bool allowDeletion)
        {
            currentAnnouncementId = announcementInfo.id;

            announcementContent.text = announcementInfo.content;
            authorName.text = announcementInfo.authorName;
            profileTag.text = $"#{announcementInfo.authorAddress[^4..]}";
            profileTag.gameObject.SetActive(!announcementInfo.authorHasClaimedName);
            verifiedMark.SetActive(announcementInfo.authorHasClaimedName);
            officialMark.SetActive(OfficialWalletsHelper.Instance.IsOfficialWallet(announcementInfo.authorAddress));
            postDate.text = TimestampUtilities.GetRelativeTimeForPosts(announcementInfo.createdAt);

            if (currentProfileThumbnailUrl != announcementInfo.authorProfilePictureUrl)
            {
                profilePicture.Setup(profileDataProvider, NameColorHelper.GetNameColor(announcementInfo.authorName), announcementInfo.authorProfilePictureUrl);
                currentProfileThumbnailUrl = announcementInfo.authorProfilePictureUrl;
            }

            likeAnnouncementButton.gameObject.SetActive(!announcementInfo.isLikedByUser);
            unlikeAnnouncementButton.gameObject.SetActive(announcementInfo.isLikedByUser);
            likesCounter.text = announcementInfo.likesCount.ToString();
            deleteAnnouncementButton.gameObject.SetActive(allowDeletion);

            RefreshCardHeight();
        }

        private void RefreshCardHeight()
        {
            announcementContent.ForceMeshUpdate(true, true);
            messageContentSizeFitter.SetLayoutVertical();
            ((RectTransform) transform).sizeDelta = new Vector2(
                ((RectTransform) transform).sizeDelta.x,
                MIN_CARD_HEIGHT + ((RectTransform) announcementContent.transform).sizeDelta.y);
        }

        private void OnLikeAnnouncementButtonClicked() =>
            LikeAnnouncementButtonClicked?.Invoke(currentAnnouncementId);

        private void OnUnlikeAnnouncementButtonClicked() =>
            UnlikeAnnouncementButtonClicked?.Invoke(currentAnnouncementId);

        private void OnDeleteAnnouncementButtonClicked()
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowDeleteConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTask ShowDeleteConfirmationDialogAsync(CancellationToken ct)
            {
                Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(
                                                                                     new ConfirmationDialogParameter(DELETE_ANNOUNCEMENT_TEXT_FORMAT, DELETE_ANNOUNCEMENT_CANCEL_TEXT, DELETE_ANNOUNCEMENT_CONFIRM_TEXT,
                                                                                         deleteSprite, false, false), ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL)
                    return;

                DeleteAnnouncementButtonClicked?.Invoke(currentAnnouncementId);
            }
        }
    }
}
