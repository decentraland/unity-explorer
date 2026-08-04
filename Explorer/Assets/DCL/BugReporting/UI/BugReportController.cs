using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utility.Types;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.BugReporting.UI
{
    /// <summary>
    ///     Drives the bug report form: validates the input, submits it through
    ///     <see cref="BugReportService" /> in the background and confirms with a success popup.
    /// </summary>
    public class BugReportController : ControllerBase<BugReportView, BugReportParams>
    {
        // The service appends coordinates and the Sentry link to the description, so the form's
        // cap stays well under the proxy's 10,000 characters-per-attribute limit.
        internal const int DESCRIPTION_MAX_LENGTH = 5000;

        private readonly BugReportService bugReportService;
        private readonly ISelfProfile selfProfile;
        private readonly World globalWorld;
        private readonly Entity playerEntity;
        private readonly IBugReportImageProvider? imageProvider;

        private BugReportImage? attachedImage;
        private UniTaskCompletionSource? closeIntent;
        private CancellationTokenSource operationsCts = new ();

        // Submissions run detached from the view lifecycle: closing the success popup must not abort the upload.
        private readonly CancellationTokenSource submissionsCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public BugReportController(
            ViewFactoryMethod viewFactory,
            BugReportService bugReportService,
            ISelfProfile selfProfile,
            World globalWorld,
            Entity playerEntity,
            IBugReportImageProvider? imageProvider = null) : base(viewFactory)
        {
            this.bugReportService = bugReportService;
            this.selfProfile = selfProfile;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
            this.imageProvider = imageProvider;
        }

        public override void Dispose()
        {
            base.Dispose();
            operationsCts.SafeCancelAndDispose();
            submissionsCts.SafeCancelAndDispose();
            ClearAttachedImage();
        }

        internal static bool CanSubmit(int issueTypeIndex, string description) =>
            issueTypeIndex >= 0 && issueTypeIndex < BugReportIssueTypes.ALL.Length && !string.IsNullOrWhiteSpace(description);

        protected override void OnViewInstantiated()
        {
            viewInstance!.IssueTypeDropdown.options.Clear();

            foreach (BugReportIssueType issueType in BugReportIssueTypes.ALL)
                viewInstance.IssueTypeDropdown.options.Add(new TMP_Dropdown.OptionData(issueType.Label));

            viewInstance.DescriptionInput.characterLimit = DESCRIPTION_MAX_LENGTH;

            viewInstance.IssueTypeDropdown.onValueChanged.AddListener(OnFormChanged);
            viewInstance.DescriptionInput.onValueChanged.AddListener(OnFormChanged);
            viewInstance.SubmitButton.onClick.AddListener(OnSubmitClicked);
            viewInstance.CancelButton.onClick.AddListener(RequestClose);
            viewInstance.CloseButton.onClick.AddListener(RequestClose);
            viewInstance.SuccessDoneButton.onClick.AddListener(RequestClose);
            viewInstance.AttachScreenshotButton.onClick.AddListener(OnAttachScreenshotClicked);
            viewInstance.RemoveScreenshotButton.onClick.AddListener(OnRemoveScreenshotClicked);

            // Without a provider there is no way to pick an image, so the section is hidden.
            viewInstance.ScreenshotSection.SetActive(imageProvider != null);
        }

        protected override void OnBeforeViewShow()
        {
            operationsCts = operationsCts.SafeRestart();
            ClearAttachedImage();

            viewInstance!.IssueTypeDropdown.SetValueWithoutNotify(-1);
            viewInstance.DescriptionInput.SetTextWithoutNotify(inputData.PrefilledDescription ?? string.Empty);
            viewInstance.ShareLogsToggle.SetIsOnWithoutNotify(true);
            viewInstance.SetScreenshot(null);
            viewInstance.ShowState(BugReportViewState.Form);
            RefreshSubmitInteractable();
        }

        protected override void OnViewClose()
        {
            operationsCts = operationsCts.SafeRestart();
            ClearAttachedImage();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeIntent = new UniTaskCompletionSource();
            await closeIntent.Task.AttachExternalCancellation(ct).SuppressCancellationThrow();
        }

        private void RequestClose() =>
            closeIntent?.TrySetResult();

        private void OnFormChanged(int _) =>
            RefreshSubmitInteractable();

        private void OnFormChanged(string _) =>
            RefreshSubmitInteractable();

        private void RefreshSubmitInteractable() =>
            viewInstance!.SubmitButton.interactable = CanSubmit(viewInstance.IssueTypeDropdown.value, viewInstance.DescriptionInput.text);

        private void OnSubmitClicked()
        {
            if (!CanSubmit(viewInstance!.IssueTypeDropdown.value, viewInstance.DescriptionInput.text))
                return;

            var draft = new BugReportDraft(
                viewInstance.IssueTypeDropdown.value,
                viewInstance.DescriptionInput.text,
                attachedImage,
                viewInstance.ShareLogsToggle.isOn);

            viewInstance.ShowState(BugReportViewState.Success);
            SubmitDetachedAsync(draft, submissionsCts.Token).Forget();
        }

        private async UniTaskVoid SubmitDetachedAsync(BugReportDraft draft, CancellationToken ct)
        {
            Result<string> result = await SubmitDraftAsync(draft, ct);

            if (!result.Success && !ct.IsCancellationRequested)
                ReportHub.LogError(ReportCategory.UNSPECIFIED, $"Bug report submission failed: {result.ErrorMessage}");
        }

        /// <summary>Exception-free: every outcome, including cancellation, arrives as a result.</summary>
        internal async UniTask<Result<string>> SubmitDraftAsync(BugReportDraft draft, CancellationToken ct)
        {
            string? userName = await GetUserNameAsync(ct);

            if (ct.IsCancellationRequested)
                return Result<string>.CancelledResult();

            var input = new BugReportInput
            {
                IssueType = BugReportIssueTypes.ALL[draft.IssueTypeIndex],
                Description = draft.Description.Trim(),
                Image = draft.Image?.Bytes,
                ImageContentType = draft.Image?.ContentType,
                ShareLogs = draft.ShareLogs,
                UserName = userName,
                Coordinates = CurrentParcel(),
            };

            return await bugReportService.SubmitAsync(input, ct);
        }

        private async UniTask<string?> GetUserNameAsync(CancellationToken ct)
        {
            try
            {
                Profile? profile = await selfProfile.ProfileAsync(ct);
                return profile?.DisplayName;
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception e)
            {
                // The name is optional on the report, so a failed lookup must not block the submission.
                ReportHub.LogException(e, ReportCategory.UNSPECIFIED);
                return null;
            }
        }

        private Vector2Int? CurrentParcel() =>
            globalWorld.TryGet(playerEntity, out CharacterTransform characterTransform) && characterTransform.Transform != null
                ? characterTransform.Position.ToParcel()
                : null;

        private void OnAttachScreenshotClicked() =>
            AttachScreenshotAsync(operationsCts.Token).Forget();

        private async UniTaskVoid AttachScreenshotAsync(CancellationToken ct)
        {
            Result<BugReportImage> picked = await imageProvider!.PickAsync(ct);

            if (ct.IsCancellationRequested)
                return;

            if (!picked.Success)
            {
                if (picked.ErrorMessage != nameof(OperationCanceledException))
                    ReportHub.LogWarning(ReportCategory.UNSPECIFIED, $"Bug report screenshot rejected: {picked.ErrorMessage}");

                return;
            }

            ClearAttachedImage();
            attachedImage = picked.Value;
            viewInstance!.SetScreenshot(picked.Value.Preview);
        }

        private void OnRemoveScreenshotClicked()
        {
            ClearAttachedImage();
            viewInstance!.SetScreenshot(null);
        }

        private void ClearAttachedImage()
        {
            if (attachedImage.HasValue)
                UnityEngine.Object.Destroy(attachedImage.Value.Preview);

            attachedImage = null;
        }
    }

    /// <summary>What the user typed into the form, captured at submit time.</summary>
    internal readonly struct BugReportDraft
    {
        public readonly int IssueTypeIndex;
        public readonly string Description;
        public readonly BugReportImage? Image;
        public readonly bool ShareLogs;

        public BugReportDraft(int issueTypeIndex, string description, BugReportImage? image, bool shareLogs)
        {
            IssueTypeIndex = issueTypeIndex;
            Description = description;
            Image = image;
            ShareLogs = shareLogs;
        }
    }
}
