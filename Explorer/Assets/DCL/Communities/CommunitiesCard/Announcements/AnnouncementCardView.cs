using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.UI.ConfirmationDialog.Opener;
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
        private const string DELETE_ANNOUNCEMENT_TEXT_FORMAT = "Are you sure you want to delete this announcement?";
        private const string DELETE_ANNOUNCEMENT_CONFIRM_TEXT = "CONTINUE";
        private const string DELETE_ANNOUNCEMENT_CANCEL_TEXT = "CANCEL";

        [SerializeField] private TMP_Text announcementContent = null!;
        [SerializeField] private TMP_Text authorName = null!;
        [SerializeField] private Button createAnnouncementButton = null!;

        private string communityId = null!;
        private string announcementId = null!;

        private CancellationTokenSource confirmationDialogCts;

        public event Action<string, string>? DeleteAnnouncementButtonClicked;

        private void Awake() =>
            createAnnouncementButton.onClick.AddListener(OnDeleteAnnouncementButtonClicked);

        private void OnDisable() =>
            confirmationDialogCts.SafeCancelAndDispose();

        private void OnDestroy() =>
            createAnnouncementButton.onClick.RemoveListener(OnDeleteAnnouncementButtonClicked);

        public void Configure(CommunityPost announcementInfo)
        {
            communityId = announcementInfo.communityId;
            announcementId = announcementInfo.id;

            announcementContent.text = announcementInfo.content;
            authorName.text = announcementInfo.authorName;

            // TODO (Santi): Avoid to use ForceRebuildLayoutImmediate removing the content size fitter and calculating the height manually
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) transform);
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

                DeleteAnnouncementButtonClicked?.Invoke(communityId, announcementId);
            }
        }
    }
}
