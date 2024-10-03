using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.EmotesWheel
{
    public class EmotesWheelController : ControllerBase<EmotesWheelView>
    {
        private const string? EMPTY_IMAGE_TYPE = "empty";
        private readonly ISelfProfile selfProfile;
        private readonly IEmoteStorage emoteStorage;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IInputBlock inputBlock;
        private readonly DCLInput.EmoteWheelActions emoteWheelInput;
        private readonly IMVCManager mvcManager;
        private readonly ICursor cursor;
        private readonly URN[] currentEmotes = new URN[Avatar.MAX_EQUIPPED_EMOTES];
        private UniTaskCompletionSource? closeViewTask;
        private CancellationTokenSource? fetchProfileCts;
        private CancellationTokenSource? slotSetUpCts;
        private readonly DCLInput dclInput;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public EmotesWheelController(ViewFactoryMethod viewFactory,
            ISelfProfile selfProfile,
            IEmoteStorage emoteStorage,
            NftTypeIconSO rarityBackgrounds,
            World world,
            Entity playerEntity,
            IThumbnailProvider thumbnailProvider,
            IInputBlock inputBlock,
            DCLInput dclInput,
            IMVCManager mvcManager,
            ICursor cursor)
            : base(viewFactory)
        {
            this.selfProfile = selfProfile;
            this.emoteStorage = emoteStorage;
            this.rarityBackgrounds = rarityBackgrounds;
            this.world = world;
            this.playerEntity = playerEntity;
            this.thumbnailProvider = thumbnailProvider;
            this.inputBlock = inputBlock;
            this.dclInput = dclInput;
            emoteWheelInput = this.dclInput.EmoteWheel;
            this.mvcManager = mvcManager;
            this.cursor = cursor;

            emoteWheelInput.Customize.performed += OpenBackpack;
            emoteWheelInput.Close.performed += Close;
            dclInput.UI.Close.performed += Close;
        }

        public override void Dispose()
        {
            base.Dispose();

            emoteWheelInput.Customize.performed -= OpenBackpack;
            emoteWheelInput.Close.performed -= Close;
            dclInput.UI.Close.performed -= Close;
            UnregisterSlotsInput(emoteWheelInput);
        }

        protected override void OnViewInstantiated()
        {
            viewInstance!.OnClose += Close;
            viewInstance.EditButton.onClick.AddListener(OpenBackpack);
            viewInstance.CurrentEmoteName.text = "";

            for (var i = 0; i < viewInstance.Slots.Length; i++)
            {
                EmoteWheelSlotView slot = viewInstance.Slots[i];
                slot.Slot = i;
                slot.OnPlay += PlayEmote;
                slot.OnHover += UpdateCurrentEmote;
                slot.OnFocusLeave += ClearCurrentEmote;
            }
        }

        protected override void OnBeforeViewShow()
        {
            UnblockUnwantedInputs();
            cursor.Unlock();
            fetchProfileCts = fetchProfileCts.SafeRestart();
            InitializeEverythingAsync(fetchProfileCts.Token).Forget();
            return;

            async UniTaskVoid InitializeEverythingAsync(CancellationToken ct)
            {
                Profile? profile = await selfProfile.ProfileAsync(ct);

                if (profile == null)
                {
                    ReportHub.LogError(new ReportData(ReportCategory.EMOTE), "Could not initialize emote wheel slots, profile is null");
                    return;
                }

                SetUpSlots(profile);
            }
        }

        protected override void OnViewShow() =>
            ListenToSlotsInput(this.dclInput.EmoteWheel);

        protected override void OnViewClose()
        {
            BlockUnwantedInputs();

            fetchProfileCts.SafeCancelAndDispose();
            slotSetUpCts.SafeCancelAndDispose();

            UnregisterSlotsInput(emoteWheelInput);
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask?.TrySetCanceled(ct);
            closeViewTask = new UniTaskCompletionSource();

            UnblockShortcutToEmoteSlotsSetup();

            await closeViewTask.Task;
        }

        private void SetUpSlots(Profile profile)
        {
            slotSetUpCts = slotSetUpCts.SafeRestart();

            for (var i = 0; i < profile.Avatar.Emotes.Count; i++)
            {
                URN urn = profile.Avatar.Emotes[i].Shorten();
                currentEmotes[i] = urn;

                if (urn.IsNullOrEmpty())
                    SetUpEmptySlot(i);
                else
                    SetUpSlotAsync(i, urn, slotSetUpCts.Token).Forget();
            }
        }

        private async UniTaskVoid SetUpSlotAsync(int slot, URN emoteUrn, CancellationToken ct)
        {
            if (!emoteStorage.TryGetElement(emoteUrn, out IEmote emote))
            {
                ReportHub.LogError(new ReportData(), $"Could not setup emote wheel slot {slot} for {emoteUrn}, missing emote in cache");
                return;
            }

            EmoteWheelSlotView view = viewInstance!.Slots[slot];

            view.BackgroundRarity.sprite = rarityBackgrounds.GetTypeImage(emote.GetRarity());
            view.EmptyContainer.SetActive(false);

            await WaitForThumbnailAsync(emote, view, ct);
        }

        private void SetUpEmptySlot(int slot)
        {
            EmoteWheelSlotView view = viewInstance!.Slots[slot];

            view.BackgroundRarity.sprite = rarityBackgrounds.GetTypeImage(EMPTY_IMAGE_TYPE);
            view.EmptyContainer.SetActive(true);
            view.Thumbnail.gameObject.SetActive(false);
        }

        private async UniTask WaitForThumbnailAsync(IEmote emote, EmoteWheelSlotView view, CancellationToken ct)
        {
            view.Thumbnail.gameObject.SetActive(false);
            view.LoadingSpinner.SetActive(true);

            Sprite? sprite = await thumbnailProvider.GetAsync(emote, ct);

            view.Thumbnail.sprite = sprite;
            view.Thumbnail.gameObject.SetActive(true);
            view.LoadingSpinner.SetActive(false);
        }

        private void UpdateCurrentEmote(int slot)
        {
            if (!emoteStorage.TryGetElement(currentEmotes[slot], out IEmote emote))
                ClearCurrentEmote(slot);
            else
                viewInstance!.CurrentEmoteName.text = emote.GetName();
        }

        private void ClearCurrentEmote(int slot)
        {
            viewInstance!.CurrentEmoteName.text = string.Empty;
        }

        private void PlayEmote(int slot)
        {
            world.AddOrGet(playerEntity, new TriggerEmoteBySlotIntent { Slot = slot });

            Close();
        }

        private void PlayEmote(InputAction.CallbackContext context)
        {
            string actionName = context.action.name;
            int slot = GetSlotFromInputName(actionName);
            PlayEmote(slot);
        }

        private void OpenBackpack(InputAction.CallbackContext context) =>
            OpenBackpack();

        private void OpenBackpack()
        {
            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(
                    new ExplorePanelParameter(ExploreSections.Backpack, BackpackSections.Emotes)));

            Close();
        }

        private void UnblockUnwantedInputs()
        {
            inputBlock.Disable(InputMapComponent.Kind.EMOTES, InputMapComponent.Kind.SHORTCUTS);
        }

        // Note: This must be called once the menu has loaded and is ready to be closed
        private void UnblockShortcutToEmoteSlotsSetup()
        {
            inputBlock.Enable(InputMapComponent.Kind.EMOTE_WHEEL);
        }

        private void BlockUnwantedInputs()
        {
            inputBlock.Disable(InputMapComponent.Kind.EMOTE_WHEEL);
            inputBlock.Enable(InputMapComponent.Kind.EMOTES, InputMapComponent.Kind.SHORTCUTS);
        }

        private void ListenToSlotsInput(InputActionMap inputActionMap)
        {
            for (var i = 0; i < Avatar.MAX_EQUIPPED_EMOTES; i++)
            {
                string actionName = GetSlotInputName(i);
                InputAction inputAction = inputActionMap.FindAction(actionName);
                inputAction.started += PlayEmote;
            }
        }

        private void UnregisterSlotsInput(InputActionMap inputActionMap)
        {
            for (var i = 0; i < Avatar.MAX_EQUIPPED_EMOTES; i++)
            {
                string actionName = GetSlotInputName(i);
                InputAction inputAction = inputActionMap.FindAction(actionName);
                inputAction.started -= PlayEmote;
            }
        }

        private void Close(InputAction.CallbackContext context) =>
            Close();

        private void Close() =>
            closeViewTask?.TrySetResult();

        private static string GetSlotInputName(int slot) =>
            $"Slot {slot}";

        private static int GetSlotFromInputName(string name) =>
            int.Parse(name[^1].ToString());
    }
}
