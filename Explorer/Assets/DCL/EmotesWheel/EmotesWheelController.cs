using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using ECS.Abstract;
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
        private readonly ISelfProfile selfProfile;
        private readonly IEmoteCache emoteCache;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly SingleInstanceEntity currentInputMapsEntity;
        private readonly DCLInput.EmoteWheelActions emoteWheelInput;
        private readonly IMVCManager mvcManager;
        private readonly URN[] currentEmotes = new URN[Avatar.MAX_EQUIPPED_EMOTES];
        private UniTaskCompletionSource? closeViewTask;
        private CancellationTokenSource? fetchProfileCts;
        private CancellationTokenSource? slotSetUpCts;
        private readonly DCLInput dclInput;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public EmotesWheelController(ViewFactoryMethod viewFactory,
            ISelfProfile selfProfile,
            IEmoteCache emoteCache,
            NftTypeIconSO rarityBackgrounds,
            World world,
            Entity playerEntity,
            IThumbnailProvider thumbnailProvider,
            SingleInstanceEntity currentInputMapsEntity,
            DCLInput dclInput,
            IMVCManager mvcManager)
            : base(viewFactory)
        {
            this.selfProfile = selfProfile;
            this.emoteCache = emoteCache;
            this.rarityBackgrounds = rarityBackgrounds;
            this.world = world;
            this.playerEntity = playerEntity;
            this.thumbnailProvider = thumbnailProvider;
            this.currentInputMapsEntity = currentInputMapsEntity;
            this.dclInput = dclInput;
            emoteWheelInput = this.dclInput.EmoteWheel;
            this.mvcManager = mvcManager;

            emoteWheelInput.Customize.performed += OpenBackpack;
            emoteWheelInput.Close.performed += Close;
            dclInput.UI.Close.performed += Close;

            ListenToSlotsInput(this.dclInput.EmoteWheel);
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
            viewInstance.OnClose += Close;
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

            fetchProfileCts = fetchProfileCts.SafeRestart();
            InitializeEverythingAsync(fetchProfileCts.Token).Forget();
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();

            EnableInputActions();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();

            DisableInputActions();

            fetchProfileCts.SafeCancelAndDispose();
            slotSetUpCts.SafeCancelAndDispose();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            closeViewTask?.TrySetCanceled(ct);
            closeViewTask = new UniTaskCompletionSource();
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
            if (!emoteCache.TryGetEmote(emoteUrn, out IEmote emote))
            {
                ReportHub.LogError(new ReportData(), $"Could not setup emote wheel slot {slot} for {emoteUrn}, missing emote in cache");
                return;
            }

            EmoteWheelSlotView view = viewInstance.Slots[slot];

            view.BackgroundRarity.sprite = rarityBackgrounds.GetTypeImage(emote.GetRarity());
            view.EmptyContainer.SetActive(false);

            await WaitForThumbnailAsync(emote, view, ct);
        }

        private void SetUpEmptySlot(int slot)
        {
            EmoteWheelSlotView view = viewInstance.Slots[slot];

            view.BackgroundRarity.sprite = rarityBackgrounds.GetTypeImage("empty");
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
            if (!emoteCache.TryGetEmote(currentEmotes[slot], out IEmote emote))
                ClearCurrentEmote(slot);
            else
                viewInstance.CurrentEmoteName.text = emote.GetName();
        }

        private void ClearCurrentEmote(int slot)
        {
            viewInstance.CurrentEmoteName.text = "";
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

        private void EnableInputActions()
        {
            ref InputMapComponent inputMapComponent = ref currentInputMapsEntity.GetInputMapComponent(world);
            inputMapComponent.Active |= InputMapComponent.Kind.EmoteWheel;
            inputMapComponent.Active &= ~InputMapComponent.Kind.Emotes;

            // We also disable shortcuts because the wheel can be opened and closed with the same key bind
            // If we leave it enabled, it will close and then re-open instantly
            inputMapComponent.Active &= ~InputMapComponent.Kind.Shortcuts;
        }

        private void DisableInputActions()
        {
            ref InputMapComponent inputMapComponent = ref currentInputMapsEntity.GetInputMapComponent(world);
            inputMapComponent.Active &= ~InputMapComponent.Kind.EmoteWheel;
            inputMapComponent.Active |= InputMapComponent.Kind.Emotes;
            inputMapComponent.Active |= InputMapComponent.Kind.Shortcuts;
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
