using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.Backpack.BackpackBus;
using System;
using System.Threading;
using Utility;

namespace DCL.Backpack
{
    public class BackpackEmoteSlotsController : IDisposable
    {
        private const int MIN_WAIT_TIME = 500;

        private readonly BackpackEventBus backpackEventBus;
        private readonly IBackpackCommandBus backpackCommandBus;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly (EmoteSlotContainerView, CancellationTokenSource)[] avatarSlots;

        private EmoteSlotContainerView? previousSlot;

        public BackpackEmoteSlotsController(
            EmoteSlotContainerView[] avatarSlotViews,
            BackpackEventBus backpackEventBus,
            IBackpackCommandBus backpackCommandBus,
            NftTypeIconSO rarityBackgrounds)
        {
            this.backpackEventBus = backpackEventBus;
            this.backpackCommandBus = backpackCommandBus;
            this.rarityBackgrounds = rarityBackgrounds;

            this.backpackEventBus.EquipEmoteEvent += EquipInSlot;
            this.backpackEventBus.UnEquipEmoteEvent += UnEquipInSlot;

            avatarSlots = new (EmoteSlotContainerView, CancellationTokenSource)[avatarSlotViews.Length];

            for (var i = 0; i < avatarSlotViews.Length; i++)
            {
                int slot = i;
                EmoteSlotContainerView avatarSlotView = avatarSlotViews[i];
                avatarSlots[i] = (avatarSlotView, new CancellationTokenSource());
                avatarSlotView.OnSlotButtonPressed += OnSlotButtonPressed;
                avatarSlotView.UnEquipButton.onClick.AddListener(() => backpackCommandBus.SendCommand(new BackpackUnEquipEmoteCommand(slot: slot)));

                UnEquipInSlot(i, null);
            }
        }

        public void Dispose()
        {
            backpackEventBus.EquipEmoteEvent -= EquipInSlot;
            backpackEventBus.UnEquipEmoteEvent -= UnEquipInSlot;

            foreach ((EmoteSlotContainerView, CancellationTokenSource) avatarSlotView in avatarSlots)
                avatarSlotView.Item1.OnSlotButtonPressed -= OnSlotButtonPressed;
        }

        private void UnEquipInSlot(int slot, IEmote? emote)
        {
            EmoteSlotContainerView avatarSlotView = avatarSlots[slot].Item1;
            CancellationTokenSource cts = avatarSlots[slot].Item2;

            cts.SafeCancelAndDispose();
            avatarSlotView.SlotWearableThumbnail.gameObject.SetActive(false);
            avatarSlotView.SlotWearableThumbnail.sprite = null;
            avatarSlotView.BackgroundRarity.sprite = null;
            avatarSlotView.EmptyOverlay.SetActive(true);
            avatarSlotView.EmptyEmoteName.gameObject.SetActive(true);
            avatarSlotView.EmoteName.gameObject.SetActive(false);
        }

        private void EquipInSlot(int slot, IEmote emote)
        {
            EmoteSlotContainerView avatarSlotView = avatarSlots[slot].Item1;
            CancellationTokenSource cts = avatarSlots[slot].Item2;

            avatarSlotView.BackgroundRarity.sprite = rarityBackgrounds.GetTypeImage(emote.GetRarity());
            avatarSlotView.EmptyOverlay.SetActive(false);
            avatarSlotView.EmptyEmoteName.gameObject.SetActive(false);
            avatarSlotView.EmoteName.gameObject.SetActive(true);
            avatarSlotView.EmoteName.text = emote.GetName();

            cts = cts.SafeRestart();
            avatarSlots[slot].Item2 = cts;

            WaitForThumbnailAsync(emote, avatarSlotView, cts.Token).Forget();
        }

        private async UniTaskVoid WaitForThumbnailAsync(IEmote emote, EmoteSlotContainerView avatarSlotView, CancellationToken ct)
        {
            avatarSlotView.StartLoadingAnimation();

            do await UniTask.Delay(MIN_WAIT_TIME, cancellationToken: ct);
            while (emote.ThumbnailAssetResult == null);

            avatarSlotView.SlotWearableThumbnail.sprite = emote.ThumbnailAssetResult.Value.Asset;
            avatarSlotView.SlotWearableThumbnail.gameObject.SetActive(true);
            avatarSlotView.LoadingView.FinishLoadingAnimation(avatarSlotView.NftContainer);
        }

        private void OnSlotButtonPressed(EmoteSlotContainerView avatarSlot)
        {
            if (previousSlot != null)
                previousSlot.SelectedBackground.SetActive(false);

            if (avatarSlot == previousSlot)
            {
                previousSlot.SelectedBackground.SetActive(false);
                previousSlot = null;
                return;
            }

            previousSlot = avatarSlot;
            avatarSlot.SelectedBackground.SetActive(true);
            backpackCommandBus.SendCommand(new BackpackEmoteSlotSelectCommand(GetSlot(avatarSlot)));
        }

        private int GetSlot(EmoteSlotContainerView view)
        {
            for (var i = 0; i < avatarSlots.Length; i++)
            {
                if (avatarSlots[i].Item1 == view)
                    return i;
            }

            return -1;
        }
    }
}
