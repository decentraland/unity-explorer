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
using System.Globalization;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Communities.CommunitiesCard.Announcements
{
    public class AnnouncementCardView : MonoBehaviour
    {
        private const string DELETE_ANNOUNCEMENT_TEXT_FORMAT = "Are you sure you want to delete this announcement?";
        private const string DELETE_ANNOUNCEMENT_CONFIRM_TEXT = "CONTINUE";
        private const string DELETE_ANNOUNCEMENT_CANCEL_TEXT = "CANCEL";

        [SerializeField] private TMP_Text announcementContent = null!;
        [SerializeField] private TMP_Text authorName = null!;
        [SerializeField] private TMP_Text profileTag = null!;
        [SerializeField] private GameObject verifiedMark = null!;
        [SerializeField] private GameObject officialMark = null!;
        [SerializeField] private TMP_Text postDate = null!;
        [SerializeField] private Button createAnnouncementButton = null!;
        [SerializeField] private ProfilePictureView profilePicture = null!;

        private string currentCommunityId = null!;
        private string currentAnnouncementId = null!;
        private string currentProfileThumbnailUrl = null!;

        private CancellationTokenSource confirmationDialogCts = null!;

        public event Action<string, string>? DeleteAnnouncementButtonClicked;

        private void Awake() =>
            createAnnouncementButton.onClick.AddListener(OnDeleteAnnouncementButtonClicked);

        private void OnDisable() =>
            confirmationDialogCts.SafeCancelAndDispose();

        private void OnDestroy() =>
            createAnnouncementButton.onClick.RemoveListener(OnDeleteAnnouncementButtonClicked);

        public void Configure(CommunityPost announcementInfo, ProfileRepositoryWrapper profileDataProvider)
        {
            currentCommunityId = announcementInfo.communityId;
            currentAnnouncementId = announcementInfo.id;

            announcementContent.text = announcementInfo.content;
            authorName.text = announcementInfo.authorName;
            profileTag.text = $"#{announcementInfo.authorAddress[^4..]}";
            profileTag.gameObject.SetActive(!announcementInfo.authorHasClaimedName);
            verifiedMark.SetActive(announcementInfo.authorHasClaimedName);
            officialMark.SetActive(OfficialWalletsHelper.Instance.IsOfficialWallet(announcementInfo.authorAddress));
            postDate.text = FormatDateString(announcementInfo.createdAt);

            if (currentProfileThumbnailUrl != announcementInfo.authorProfilePictureUrl)
            {
                profilePicture.Setup(profileDataProvider, NameColorHelper.GetNameColor(announcementInfo.authorName), announcementInfo.authorProfilePictureUrl);
                currentProfileThumbnailUrl = announcementInfo.authorProfilePictureUrl;
            }

            // TODO (Santi): Avoid to use ForceRebuildLayoutImmediate removing the content size fitter and calculating the height manually
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) transform);
        }

        private static string FormatDateString(string createdAtDateString)
        {
            DateTime announcementDateTime = DateTime.Parse(createdAtDateString, null, DateTimeStyles.RoundtripKind);
            TimeSpan timeDifference = DateTime.UtcNow - announcementDateTime;

            if (timeDifference.TotalMinutes < 1)
                return "Now";

            switch (timeDifference.TotalHours)
            {
                case < 1:
                {
                    int minutes = (int)Math.Floor(timeDifference.TotalMinutes);
                    return $"{minutes}m";
                }
                case < 24:
                {
                    int hours = (int)Math.Floor(timeDifference.TotalHours);
                    return $"{hours}h";
                }
                default:
                    return announcementDateTime.ToString(announcementDateTime.Year == DateTime.UtcNow.Year ? "MMM d" : "MMM d, yyyy", CultureInfo.InvariantCulture);
            }
        }

        private void OnDeleteAnnouncementButtonClicked()
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowDeleteConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTask ShowDeleteConfirmationDialogAsync(CancellationToken ct)
            {
                Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(
                                                                                     new ConfirmationDialogParameter(
                                                                                         DELETE_ANNOUNCEMENT_TEXT_FORMAT,
                                                                                         DELETE_ANNOUNCEMENT_CANCEL_TEXT,
                                                                                         DELETE_ANNOUNCEMENT_CONFIRM_TEXT,
                                                                                         null, true, false),
                                                                                     ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL)
                    return;

                DeleteAnnouncementButtonClicked?.Invoke(currentCommunityId, currentAnnouncementId);
            }
        }
    }
}
