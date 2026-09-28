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
using System.Collections.Generic;
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
        // Leaves headroom under the proxy's 10,000 characters-per-attribute cap for its HTML escaping and the appended metadata.
        internal const int DESCRIPTION_MAX_LENGTH = 2500;

        private const int ABOVE_OVERLAYS_ORDER = 100;

        private readonly BugReportService bugReportService;
        private readonly ISelfProfile selfProfile;
        private readonly IInputBlock inputBlock;
        private readonly World globalWorld;
        private readonly Entity playerEntity;
        private readonly IBugReportImageProvider? imageProvider;
        private readonly IBugReportSessionContext? sessionContext;

        private readonly List<BugReportImage> attachedImages = new (IntercomTicketPayload.MAX_EVIDENCE_IMAGES);

        private UniTaskCompletionSource? closeIntent;
        private CancellationTokenSource operationsCts = new ();

        // Detached from the view lifecycle: closing the success popup must not abort the upload.
        private readonly CancellationTokenSource submissionsCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        /// <summary>The prefab may offer fewer slots than the proxy accepts, never more.</summary>
        private int maxScreenshots => Mathf.Min(IntercomTicketPayload.MAX_EVIDENCE_IMAGES, viewInstance!.ScreenshotSlots.Length);

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
            ClearAttachedImages();
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
            viewInstance!.Initialize();

            viewInstance.IssueTypeDropdown.options.Clear();

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

            for (var i = 0; i < viewInstance.ScreenshotSlots.Length; i++)
            {
                int slotIndex = i;
                viewInstance.ScreenshotSlots[i].RemoveButton.onClick.AddListener(() => OnRemoveScreenshotClicked(slotIndex));
            }

            viewInstance.ScreenshotSection.SetActive(imageProvider != null);
        }

        protected override void OnBeforeViewShow()
        {
            operationsCts = operationsCts.SafeRestart();
            ClearAttachedImages();

            viewInstance!.IssueTypeDropdown.SetValueWithoutNotify(IssueTypeIndexOf(inputData.PrefilledIssueType));
            viewInstance.DescriptionInput.SetTextWithoutNotify(inputData.PrefilledDescription ?? string.Empty);
            viewInstance.HideCharCounter();
            viewInstance.ShareLogsToggle.SetIsOnWithoutNotify(true);
            RefreshScreenshots();
            viewInstance.ShowState(BugReportViewState.Form);
            RefreshSubmitInteractable();
        }

        protected override void OnViewShow()
        {
            inputBlock.Disable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera, InputMapComponent.Kind.Camera, InputMapComponent.Kind.Player);

            // Popups draw behind Overlay views, so only the draw order is raised; the MVC stack reassigns it on every show.
            if (inputData.ShowAboveOverlays)
                viewInstance!.SetDrawOrder(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, ABOVE_OVERLAYS_ORDER));
        }

        protected override void OnViewClose()
        {
            operationsCts = operationsCts.SafeRestart();
            ClearAttachedImages();
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
            viewInstance!.SetSubmitInteractable(CanSubmit(viewInstance.IssueTypeDropdown.value, viewInstance.DescriptionInput.text, viewInstance.ShareLogsToggle.isOn));

        private void OnSubmitClicked()
        {
            if (!CanSubmit(viewInstance!.IssueTypeDropdown.value, viewInstance.DescriptionInput.text, viewInstance.ShareLogsToggle.isOn))
                return;

            var draft = new BugReportDraft(
                viewInstance.IssueTypeDropdown.value,
                viewInstance.DescriptionInput.text,
                attachedImages.ToArray());

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

            var images = new EvidenceImage[draft.Images.Count];

            for (var i = 0; i < images.Length; i++)
                images[i] = new EvidenceImage(draft.Images[i].Bytes, draft.Images[i].ContentType);

            var input = new BugReportInput
            {
                IssueType = BugReportIssueTypes.ALL[draft.IssueTypeIndex],
                Description = draft.Description.Trim(),
                Images = images,
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

            if (attachedImages.Count >= maxScreenshots)
            {
                UnityEngine.Object.Destroy(picked.Value.Preview);
                return;
            }

            attachedImages.Add(picked.Value);
            RefreshScreenshots();
        }

        private void OnRemoveScreenshotClicked(int slotIndex)
        {
            if (slotIndex >= attachedImages.Count)
                return;

            UnityEngine.Object.Destroy(attachedImages[slotIndex].Preview);
            attachedImages.RemoveAt(slotIndex);
            RefreshScreenshots();
        }

        private void RefreshScreenshots() =>
            viewInstance!.SetScreenshots(attachedImages, attachedImages.Count < maxScreenshots);

        private void ClearAttachedImages()
        {
            foreach (BugReportImage image in attachedImages)
                UnityEngine.Object.Destroy(image.Preview);

            attachedImages.Clear();
        }
    }

    internal readonly struct BugReportDraft
    {
        public readonly int IssueTypeIndex;
        public readonly string Description;

        /// <summary>A snapshot: the controller's own list is cleared when the view closes while the upload is still running.</summary>
        public readonly IReadOnlyList<BugReportImage> Images;

        public BugReportDraft(int issueTypeIndex, string description, IReadOnlyList<BugReportImage> images)
        {
            IssueTypeIndex = issueTypeIndex;
            Description = description;
            Images = images;
        }
    }
}
