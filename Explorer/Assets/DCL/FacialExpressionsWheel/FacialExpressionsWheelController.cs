using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.Components;
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

        private NumericCyclerController? eyebrowsCycler;
        private NumericCyclerController? eyesCycler;
        private NumericCyclerController? mouthCycler;

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

            eyebrowsCycler?.Dispose();
            eyesCycler?.Dispose();
            mouthCycler?.Dispose();

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

            eyebrowsCycler = new NumericCyclerController(viewInstance.EyebrowsCycler, CHANNEL_ATLAS_SIZE);
            eyesCycler = new NumericCyclerController(viewInstance.EyesCycler, CHANNEL_ATLAS_SIZE);
            mouthCycler = new NumericCyclerController(viewInstance.MouthCycler, CHANNEL_ATLAS_SIZE);

            eyebrowsCycler.OnIndexChanged += OnChannelCycled;
            eyesCycler.OnIndexChanged += OnChannelCycled;
            mouthCycler.OnIndexChanged += OnChannelCycled;
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

                int eb = 0, ey = 0, mo = 0;

                if (world.IsAlive(playerEntity) && world.Has<AvatarFaceComponent>(playerEntity))
                {
                    AvatarFaceComponent face = world.Get<AvatarFaceComponent>(playerEntity);
                    eb = face.EyebrowsExpressionIndex;
                    ey = face.EyesExpressionIndex;
                    mo = face.MouthExpressionIndex;
                }

                eyebrowsCycler!.SetIndex(eb);
                eyesCycler!.SetIndex(ey);
                mouthCycler!.SetIndex(mo);

                int matchedSlot = FindMatchingExpressionSlot(eb, ey, mo);
                HighlightSlot(matchedSlot);

                viewInstance!.CurrentExpressionName.text = matchedSlot >= 0 ? expressionConfig.Expressions[matchedSlot].Name : "Custom";

                // Preview avatar's AvatarFaceComponent is added async by AvatarFacialExpressionSystem
                // after wearables instantiate. Wait for it before pushing the seed indices, otherwise
                // TrySetFace silently no-ops on first open.
                await previewController.SetFaceWhenReadyAsync(eb, ey, mo, ct);
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
                FacialExpressionApplier.Apply(world, playerEntity, (byte)eyebrowsCycler!.CurrentIndex, (byte)eyesCycler!.CurrentIndex, (byte)mouthCycler!.CurrentIndex);
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
            viewInstance!.CurrentExpressionName.text = CurrentExpressionLabel();

        private void OnChannelCycled(int _)
        {
            int eb = eyebrowsCycler!.CurrentIndex;
            int ey = eyesCycler!.CurrentIndex;
            int mo = mouthCycler!.CurrentIndex;

            int matchedSlot = FindMatchingExpressionSlot(eb, ey, mo);
            HighlightSlot(matchedSlot);

            viewInstance!.CurrentExpressionName.text = matchedSlot >= 0
                ? expressionConfig.Expressions[matchedSlot].Name
                : "Custom";

            previewController.SetFace(eb, ey, mo);
            pendingChanged = true;
        }

        private string CurrentExpressionLabel()
        {
            int matched = FindMatchingExpressionSlot(eyebrowsCycler!.CurrentIndex, eyesCycler!.CurrentIndex, mouthCycler!.CurrentIndex);
            return matched >= 0 ? expressionConfig.Expressions[matched].Name : "Custom";
        }

        private int FindMatchingExpressionSlot(int eb, int ey, int mo)
        {
            for (var i = 0; i < expressionConfig.Expressions.Length; i++)
            {
                AvatarFaceExpressionDefinition def = expressionConfig.Expressions[i];
                if (def.EyebrowsIndex == eb && def.EyesIndex == ey && def.MouthIndex == mo)
                    return i;
            }

            return -1;
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

            eyebrowsCycler!.SetIndex(def.EyebrowsIndex);
            eyesCycler!.SetIndex(def.EyesIndex);
            mouthCycler!.SetIndex(def.MouthIndex);

            previewController.SetFace(def.EyebrowsIndex, def.EyesIndex, def.MouthIndex);

            viewInstance!.CurrentExpressionName.text = def.Name;
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
