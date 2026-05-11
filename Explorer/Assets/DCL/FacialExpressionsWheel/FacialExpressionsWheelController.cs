using Arch.Core;
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
        // Atlas grid is 4x4 = 16 slices per channel (see AvatarFacialExpressionConstants).
        private const int CHANNEL_ATLAS_SIZE = 16;

        private const int SLOT_COUNT = 10;

        private readonly SelfProfile selfProfile;
        private readonly AvatarFaceExpressionConfig expressionConfig;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IInputBlock inputBlock;
        private readonly ICursor cursor;
        private readonly IEventBus eventBus;
        private readonly FacialExpressionsCharacterPreviewController previewController;
        private readonly InputActionMap wheelInput;

        private UniTaskCompletionSource? closeViewTask;
        private CancellationTokenSource? fetchProfileCts;

        private int pendingEyebrowsIndex;
        private int pendingEyesIndex;
        private int pendingMouthIndex;

        // True once the user has touched a slot or cycler. Gates the on-close commit so opening
        // and closing without picking anything leaves the avatar untouched.
        private bool pendingChanged;

        private int selectedSlot = -1;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

        public FacialExpressionsWheelController(
            ViewFactoryMethod viewFactory,
            SelfProfile selfProfile,
            AvatarFaceExpressionConfig expressionConfig,
            World world,
            Entity playerEntity,
            IInputBlock inputBlock,
            ICursor cursor,
            IEventBus eventBus,
            FacialExpressionsCharacterPreviewController previewController)
            : base(viewFactory)
        {
            this.selfProfile = selfProfile;
            this.expressionConfig = expressionConfig;
            this.world = world;
            this.playerEntity = playerEntity;
            this.inputBlock = inputBlock;
            this.cursor = cursor;
            this.eventBus = eventBus;
            this.previewController = previewController;

            wheelInput = DCLInput.Instance.FaceExpressionsWheel;
        }

        public override void Dispose()
        {
            base.Dispose();

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
                FacialExpressionApplier.Apply(world, playerEntity, (byte)pendingEyebrowsIndex, (byte)pendingEyesIndex, (byte)pendingMouthIndex);
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