using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.FacialExpression;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.FacialExpressionsWheel
{
    public class FacialExpressionsWheelController : ControllerBase<FacialExpressionsWheelView>
    {
        // Mirrors EmoteWheelShortcutHandler.QUICK_EMOTE_LOCK_TIME so a fast Y release after
        // a slot click doesn't immediately re-toggle the wheel.
        private const float QUICK_APPLY_LOCK_TIME = 0.5f;

        // Atlas grid is 4x4 = 16 slices per channel (see AvatarFacialExpressionConstants).
        private const int CHANNEL_ATLAS_SIZE = 16;

        private const int SLOT_COUNT = 10;

        private readonly SelfProfile selfProfile;
        private readonly AvatarFaceExpressionConfig expressionConfig;
        private readonly IFacialExpressionApplier applier;
        private readonly IInputBlock inputBlock;
        private readonly ICursor cursor;
        private readonly IMVCManager mvcManager;
        private readonly IEventBus eventBus;
        private readonly FacialExpressionsCharacterPreviewController previewController;
        private readonly DCLInput.ShortcutsActions shortcutsInput;
        private readonly InputActionMap faceExpressionsInput;
        private readonly InputActionMap wheelInput;

        private UniTaskCompletionSource? closeViewTask;
        private CancellationTokenSource? fetchProfileCts;

        private int pendingEyebrowsIndex;
        private int pendingEyesIndex;
        private int pendingMouthIndex;

        // Y-release coordination: Y+N consumed the release, or a slot click locks it briefly.
        private bool ignoreNextRelease;
        private float lockUntilTime;

        // True once the user has touched a slot or cycler. Gates the on-close commit so opening
        // and closing without picking anything leaves the avatar untouched. Also reset to false
        // after Y+N quick-apply (face already on avatar) and on tab-swap (discard).
        private bool pendingChanged;

        private int selectedSlot = -1;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

        public FacialExpressionsWheelController(
            ViewFactoryMethod viewFactory,
            SelfProfile selfProfile,
            AvatarFaceExpressionConfig expressionConfig,
            IFacialExpressionApplier applier,
            IInputBlock inputBlock,
            ICursor cursor,
            IMVCManager mvcManager,
            IEventBus eventBus,
            FacialExpressionsCharacterPreviewController previewController)
            : base(viewFactory)
        {
            this.selfProfile = selfProfile;
            this.expressionConfig = expressionConfig;
            this.applier = applier;
            this.inputBlock = inputBlock;
            this.cursor = cursor;
            this.mvcManager = mvcManager;
            this.eventBus = eventBus;
            this.previewController = previewController;

            DCLInput input = DCLInput.Instance;
            shortcutsInput = input.Shortcuts;
            faceExpressionsInput = input.FaceExpressions;
            wheelInput = input.FaceExpressionsWheel;

            shortcutsInput.FaceExpression.canceled += OnFaceExpressionShortcutReleased;
            ListenToSlotsInput(faceExpressionsInput, OnFaceExpressionsQuickApply);
        }

        public override void Dispose()
        {
            base.Dispose();

            shortcutsInput.FaceExpression.canceled -= OnFaceExpressionShortcutReleased;
            UnregisterSlotsInput(faceExpressionsInput, OnFaceExpressionsQuickApply);
            UnregisterSlotsInput(wheelInput, OnSlotNumberPressed);

            fetchProfileCts.SafeCancelAndDispose();
            previewController.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.Closed += Close;
            viewInstance.EmotesTabButton.onClick.AddListener(SwapToEmotes);
            viewInstance.CurrentExpressionName.text = string.Empty;

            for (var i = 0; i < viewInstance.Slots.Length; i++)
            {
                Sprite icon = i < expressionConfig.Expressions.Length
                    ? expressionConfig.Expressions[i].Icon
                    : null!;

                viewInstance.Slots[i].Setup(i, icon, OnSlotPlay, OnSlotHover, OnSlotFocusLeave);
            }

            viewInstance.EyebrowsCycler.OnCycle += delta => CycleChannel(ref pendingEyebrowsIndex, delta, viewInstance.EyebrowsCycler);
            viewInstance.EyesCycler.OnCycle += delta => CycleChannel(ref pendingEyesIndex, delta, viewInstance.EyesCycler);
            viewInstance.MouthCycler.OnCycle += delta => CycleChannel(ref pendingMouthIndex, delta, viewInstance.MouthCycler);
        }

        protected override void OnBeforeViewShow()
        {
            pendingChanged = false;
            selectedSlot = -1;

            // Enable the wheel-only number map (1-9/0 + Esc) and quiet the gameplay maps that would
            // otherwise compete with mouse focus on the wheel UI.
            inputBlock.Disable(InputMapComponent.Kind.EMOTES);
            inputBlock.Disable(InputMapComponent.Kind.EMOTE_WHEEL);
            inputBlock.Enable(InputMapComponent.Kind.FACE_EXPRESSIONS_WHEEL);

            cursor.Unlock();

            ListenToSlotsInput(wheelInput, OnSlotNumberPressed);

            previewController.OnBeforeShow();

            fetchProfileCts = fetchProfileCts.SafeRestart();
            InitializePreviewAsync(fetchProfileCts.Token).Forget();
            return;

            async UniTaskVoid InitializePreviewAsync(CancellationToken ct)
            {
                Profile? profile = selfProfile.OwnProfile ?? await selfProfile.ProfileAsync(ct);

                if (profile == null)
                {
                    ReportHub.LogError(new ReportData(ReportCategory.AVATAR),
                        "Could not initialize facial expressions wheel preview, profile is null");
                    return;
                }

                previewController.Initialize(profile.Avatar, Vector3.zero);
                previewController.OnShow();

                ApplyExpressionToPreview(slot: 0);
            }
        }

        protected override void OnViewClose()
        {
            // Restore gameplay input maps. Counter-balance every Disable in OnBeforeViewShow so
            // open/close cycles don't leak the FACE_EXPRESSIONS_WHEEL/EMOTE_WHEEL block counters.
            inputBlock.Disable(InputMapComponent.Kind.FACE_EXPRESSIONS_WHEEL);
            inputBlock.Enable(InputMapComponent.Kind.EMOTES);
            inputBlock.Enable(InputMapComponent.Kind.EMOTE_WHEEL);

            UnregisterSlotsInput(wheelInput, OnSlotNumberPressed);

            previewController.OnHide();

            if (pendingChanged)
                applier.Apply((byte)pendingEyebrowsIndex, (byte)pendingEyesIndex, (byte)pendingMouthIndex);
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask?.TrySetCanceled(ct);
            closeViewTask = new UniTaskCompletionSource();
            return closeViewTask.Task.AttachExternalCancellation(ct);
        }

        public void Close() =>
            closeViewTask?.TrySetResult();

        private void OnSlotPlay(int slot)
        {
            ApplyExpressionToPreview(slot);
            HighlightSlot(slot);
            pendingChanged = true;
            lockUntilTime = Time.time + QUICK_APPLY_LOCK_TIME;
        }

        private void OnSlotHover(int slot) =>
            viewInstance!.CurrentExpressionName.text = expressionConfig.Expressions[slot].Name;

        private void OnSlotFocusLeave(int _) =>
            viewInstance!.CurrentExpressionName.text = selectedSlot >= 0
                ? expressionConfig.Expressions[selectedSlot].Name
                : string.Empty;

        private void CycleChannel(ref int currentIndex, int delta, FaceChannelCyclerView cyclerView)
        {
            currentIndex = FacialExpressionWheelUtils.WrapChannelIndex(currentIndex, delta, CHANNEL_ATLAS_SIZE);
            cyclerView.SetIndex(currentIndex + 1, CHANNEL_ATLAS_SIZE);

            HighlightSlot(-1);
            viewInstance!.CurrentExpressionName.text = string.Empty;

            previewController.SetFace(pendingEyebrowsIndex, pendingEyesIndex, pendingMouthIndex);
            pendingChanged = true;
        }

        private void OnSlotNumberPressed(InputAction.CallbackContext context)
        {
            int slot = FacialExpressionWheelUtils.SlotIndexFromActionName(context.action.name);
            ApplyExpressionToPreview(slot);
            HighlightSlot(slot);
            pendingChanged = true;
        }

        private void OnFaceExpressionsQuickApply(InputAction.CallbackContext context)
        {
            int slot = FacialExpressionWheelUtils.SlotIndexFromActionName(context.action.name);
            AvatarFaceExpressionDefinition def = expressionConfig.Expressions[slot];

            applier.Apply((byte)def.EyebrowsIndex, (byte)def.EyesIndex, (byte)def.MouthIndex);

            if (State != ControllerState.ViewHidden && State != ControllerState.ViewHiding)
            {
                // Face already applied via shortcut; skip the on-close commit.
                pendingChanged = false;
                Close();
            }
            else
            {
                ignoreNextRelease = true;
            }
        }

        private void OnFaceExpressionShortcutReleased(InputAction.CallbackContext _)
        {
            if (ignoreNextRelease)
            {
                ignoreNextRelease = false;
                return;
            }

            if (Time.time < lockUntilTime)
            {
                lockUntilTime = 0f;
                return;
            }

            if (State != ControllerState.ViewHidden && State != ControllerState.ViewHiding)
                Close();
            else
                mvcManager.ShowAndForget(IssueCommand());
        }

        private void SwapToEmotes()
        {
            // Swap discards pending face changes; user is bailing out of the face wheel.
            pendingChanged = false;
            Close();
            eventBus.Publish(new RequestToggleEmoteWheelEvent());
        }

        private void ApplyExpressionToPreview(int slot)
        {
            AvatarFaceExpressionDefinition def = expressionConfig.Expressions[slot];
            pendingEyebrowsIndex = def.EyebrowsIndex;
            pendingEyesIndex = def.EyesIndex;
            pendingMouthIndex = def.MouthIndex;

            previewController.SetFace(pendingEyebrowsIndex, pendingEyesIndex, pendingMouthIndex);

            viewInstance!.EyebrowsCycler.SetIndex(pendingEyebrowsIndex + 1, CHANNEL_ATLAS_SIZE);
            viewInstance.EyesCycler.SetIndex(pendingEyesIndex + 1, CHANNEL_ATLAS_SIZE);
            viewInstance.MouthCycler.SetIndex(pendingMouthIndex + 1, CHANNEL_ATLAS_SIZE);
            viewInstance.CurrentExpressionName.text = def.Name;
        }

        private void HighlightSlot(int slot)
        {
            if (selectedSlot == slot) return;

            if (selectedSlot >= 0 && selectedSlot < viewInstance!.Slots.Length)
                viewInstance.Slots[selectedSlot].SetSelected(false);

            selectedSlot = slot;

            if (slot >= 0 && slot < viewInstance!.Slots.Length)
                viewInstance.Slots[slot].SetSelected(true);
        }

        private static void ListenToSlotsInput(InputActionMap map, Action<InputAction.CallbackContext> handler)
        {
            for (var i = 0; i < SLOT_COUNT; i++)
                map.FindAction(FacialExpressionWheelUtils.GetSlotActionName(i)).started += handler;
        }

        private static void UnregisterSlotsInput(InputActionMap map, Action<InputAction.CallbackContext> handler)
        {
            for (var i = 0; i < SLOT_COUNT; i++)
                map.FindAction(FacialExpressionWheelUtils.GetSlotActionName(i)).started -= handler;
        }
    }
}