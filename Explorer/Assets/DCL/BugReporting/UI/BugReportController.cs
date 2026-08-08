using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
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
        // The proxy HTML-escapes and formats the description (newlines become <br>, & becomes
        // &amp;...) and enforces its 10,000 characters-per-attribute cap on that inflated text,
        // with coordinates and the Sentry link appended by the service on top. The form's cap
        // leaves room for that inflation even on escape-heavy input like pasted logs.
        internal const int DESCRIPTION_MAX_LENGTH = 2500;

        // Well above the order overlays are pushed with (1), so the boosted form clears them all.
        private const int ABOVE_OVERLAYS_ORDER = 100;

        private readonly BugReportService bugReportService;
        private readonly ISelfProfile selfProfile;
        private readonly IInputBlock inputBlock;
        private readonly World globalWorld;
        private readonly Entity playerEntity;
        private readonly IBugReportImageProvider? imageProvider;
        private readonly IBugReportSessionContext? sessionContext;

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
            IInputBlock inputBlock,
            World globalWorld,
            Entity playerEntity,
            IBugReportImageProvider? imageProvider = null,
            IBugReportSessionContext? sessionContext = null) : base(viewFactory)
        {
            this.bugReportService = bugReportService;
            this.selfProfile = selfProfile;
            this.inputBlock = inputBlock;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
            this.imageProvider = imageProvider;
            this.sessionContext = sessionContext;
        }

        public override void Dispose()
        {
            base.Dispose();
            operationsCts.SafeCancelAndDispose();
            submissionsCts.SafeCancelAndDispose();
            ClearAttachedImage();
        }

        /// <summary>
        ///     The logs toggle is a required agreement: every report ships the client log, so a
        ///     report cannot be submitted with it unchecked.
        /// </summary>
        internal static bool CanSubmit(int issueTypeIndex, string description, bool shareLogs) =>
            issueTypeIndex >= 0 && issueTypeIndex < BugReportIssueTypes.ALL.Length && !string.IsNullOrWhiteSpace(description) && shareLogs;

        /// <returns>The dropdown index of the issue type, or -1 ("no selection") when it is null or unknown.</returns>
        internal static int IssueTypeIndexOf(BugReportIssueType? issueType)
        {
            if (issueType == null)
                return -1;

            for (var i = 0; i < BugReportIssueTypes.ALL.Length; i++)
                if (BugReportIssueTypes.ALL[i].OptionId == issueType.Value.OptionId)
                    return i;

            return -1;
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.IssueTypeDropdown.options.Clear();

            foreach (BugReportIssueType issueType in BugReportIssueTypes.ALL)
                viewInstance.IssueTypeDropdown.options.Add(new TMP_Dropdown.OptionData(issueType.Label));

            viewInstance.DescriptionInput.characterLimit = DESCRIPTION_MAX_LENGTH;

            viewInstance.IssueTypeDropdown.onValueChanged.AddListener(OnFormChanged);
            viewInstance.DescriptionInput.onValueChanged.AddListener(OnFormChanged);
            viewInstance.ShareLogsToggle.onValueChanged.AddListener(OnFormChanged);
            viewInstance.SubmitButton.onClick.AddListener(OnSubmitClicked);
            viewInstance.CancelButton.onClick.AddListener(RequestClose);
            viewInstance.CloseButton.onClick.AddListener(RequestClose);
            viewInstance.SuccessDoneButton.onClick.AddListener(RequestClose);
            viewInstance.AttachScreenshotButton.onClick.AddListener(OnAttachScreenshotClicked);
            viewInstance.RemoveScreenshotButton.onClick.AddListener(OnRemoveScreenshotClicked);

            viewInstance.ScreenshotSection.SetActive(imageProvider != null);
        }

        protected override void OnBeforeViewShow()
        {
            operationsCts = operationsCts.SafeRestart();
            ClearAttachedImage();

            viewInstance!.IssueTypeDropdown.SetValueWithoutNotify(IssueTypeIndexOf(inputData.PrefilledIssueType));
            viewInstance.DescriptionInput.SetTextWithoutNotify(inputData.PrefilledDescription ?? string.Empty);
            viewInstance.HideCharCounter();
            viewInstance.ShareLogsToggle.SetIsOnWithoutNotify(true);
            viewInstance.SetScreenshot(null);
            viewInstance.ShowState(BugReportViewState.Form);
            RefreshSubmitInteractable();
        }

        protected override void OnViewShow()
        {
            inputBlock.Disable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera, InputMapComponent.Kind.Camera, InputMapComponent.Kind.Player);

            // The MVC stack keeps the form a popup (escape, modality), but a popup draws behind
            // Overlay views: when the entry point lives on one, only the draw order is raised.
            // The stack reassigns the popup ordering on every show, so this needs no undoing.
            if (inputData.ShowAboveOverlays)
                viewInstance!.SetDrawOrder(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, ABOVE_OVERLAYS_ORDER));
        }

        protected override void OnViewClose()
        {
            operationsCts = operationsCts.SafeRestart();
            ClearAttachedImage();
            inputBlock.Enable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera, InputMapComponent.Kind.Camera, InputMapComponent.Kind.Player);
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

        private void OnFormChanged(bool _) =>
            RefreshSubmitInteractable();

        private void RefreshSubmitInteractable() =>
            viewInstance!.SubmitButton.interactable = CanSubmit(viewInstance.IssueTypeDropdown.value, viewInstance.DescriptionInput.text, viewInstance.ShareLogsToggle.isOn);

        private void OnSubmitClicked()
        {
            if (!CanSubmit(viewInstance!.IssueTypeDropdown.value, viewInstance.DescriptionInput.text, viewInstance.ShareLogsToggle.isOn))
                return;

            var draft = new BugReportDraft(
                viewInstance.IssueTypeDropdown.value,
                viewInstance.DescriptionInput.text,
                attachedImage);

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
                UserName = userName,
                Coordinates = CurrentParcel(),
                MeetsMinimumSpecs = sessionContext?.MeetsMinimumSpecs,
                SceneSdkVersion = sessionContext?.SceneSdkVersion,
                LauncherVersion = sessionContext?.LauncherVersion,
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
            {
                // The view is gone: a picked preview has no owner left to destroy it later.
                if (picked.Success)
                    UnityEngine.Object.Destroy(picked.Value.Preview);

                return;
            }

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

        public BugReportDraft(int issueTypeIndex, string description, BugReportImage? image)
        {
            IssueTypeIndex = issueTypeIndex;
            Description = description;
            Image = image;
        }
    }
}
