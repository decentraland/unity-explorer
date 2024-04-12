using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.UI;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Backpack.EmotesSection
{
    public class BackpackEmoteGridController : IDisposable
    {
        private const int CURRENT_PAGE_SIZE = 16;
        private const string EMOTE_CATEGORY = "emote";

        private readonly BackpackGridView view;
        private readonly BackpackCommandBus commandBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly PageSelectorController pageSelectorController;
        private readonly Dictionary<URN, BackpackEmoteGridItemView> usedPoolItems;
        private readonly BackpackEmoteGridItemView?[] loadingResults = new BackpackEmoteGridItemView[CURRENT_PAGE_SIZE];
        private readonly IObjectPool<BackpackEmoteGridItemView> gridItemsPool;
        private readonly IEmoteProvider emoteProvider;
        private readonly IReadOnlyCollection<URN> embeddedEmoteIds;

        private CancellationTokenSource? loadElementsCancellationToken;
        private string? currentCategory;
        private string? currentSearch;
        private bool onChainEmotesOnly;
        private IEmoteProvider.OrderOperation currentOrder = new ("date", false);
        private BodyShape currentBodyShape = BodyShape.MALE;

        public BackpackEmoteGridController(
            BackpackGridView view,
            BackpackCommandBus commandBus,
            BackpackEventBus eventBus,
            IWeb3IdentityCache web3IdentityCache,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IEquippedEmotes equippedEmotes,
            BackpackSortController backpackSortController,
            PageButtonView pageButtonView,
            IObjectPool<BackpackEmoteGridItemView> gridItemsPool,
            IEmoteProvider emoteProvider,
            IReadOnlyCollection<URN> embeddedEmoteIds)
        {
            this.view = view;
            this.commandBus = commandBus;
            this.web3IdentityCache = web3IdentityCache;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.equippedEmotes = equippedEmotes;
            this.gridItemsPool = gridItemsPool;
            this.emoteProvider = emoteProvider;
            this.embeddedEmoteIds = embeddedEmoteIds;
            pageSelectorController = new PageSelectorController(view.PageSelectorView, pageButtonView);

            usedPoolItems = new Dictionary<URN, BackpackEmoteGridItemView>();
            eventBus.EquipEmoteEvent += OnEquip;
            eventBus.EquipWearableEvent += OnWearableEquipped;
            eventBus.UnEquipEmoteEvent += OnUnequip;
            eventBus.FilterCategoryEvent += OnFilterCategory;
            eventBus.SearchEvent += OnSearch;
            backpackSortController.OnSortChanged += OnSortChanged;
            backpackSortController.OnCollectiblesOnlyChanged += OnCollectiblesOnlyChanged;
            pageSelectorController.OnSetPage += RequestAndFillEmotes;
        }

        public void Dispose()
        {
            loadElementsCancellationToken.SafeCancelAndDispose();
        }

        public static async UniTask<ObjectPool<BackpackEmoteGridItemView>> InitializeAssetsAsync(IAssetsProvisioner assetsProvisioner,
            BackpackGridView view, CancellationToken ct)
        {
            BackpackEmoteGridItemView backpackItem = (await assetsProvisioner.ProvideMainAssetAsync(view.EmoteGridItem, ct: ct)).Value;

            return new ObjectPool<BackpackEmoteGridItemView>(
                () => Object.Instantiate(backpackItem, view.gameObject.transform),
                defaultCapacity: CURRENT_PAGE_SIZE
            );
        }

        private void RequestAndFillEmotes(int pageNumber)
        {
            RequestAndFillEmotes(pageNumber, false);
        }

        public void RequestAndFillEmotes(int pageNumber, bool reconfigurePageSelector)
        {
            loadElementsCancellationToken = loadElementsCancellationToken.SafeRestart();

            SetGridAsLoading();

            async UniTaskVoid RequestPageAsync(CancellationToken ct)
            {
                IReadOnlyList<IEmote> emotes;

                (IReadOnlyList<IEmote>? customOwnedEmotes, int totalAmount) = await emoteProvider.GetOwnedEmotesAsync(web3IdentityCache.Identity!.Address,
                    pageNum: pageNumber, pageSize: CURRENT_PAGE_SIZE,
                    orderOperation: currentOrder,
                    name: currentSearch,
                    ct: ct);

                if (onChainEmotesOnly)
                    emotes = customOwnedEmotes;
                else
                {
                    IReadOnlyList<IEmote> embeddedEmotes = await emoteProvider.GetEmotesAsync(embeddedEmoteIds, currentBodyShape, ct);
                    IEnumerable<IEmote> filteredEmotes = embeddedEmotes;

                    if (!string.IsNullOrEmpty(currentSearch))
                        filteredEmotes = embeddedEmotes.Where(emote => emote.GetName().Contains(currentSearch));

                    if (!string.IsNullOrEmpty(currentCategory))
                        filteredEmotes = embeddedEmotes.Where(emote => emote.GetCategory() == currentCategory);

                    filteredEmotes = currentOrder.By switch
                                     {
                                         "name" => currentOrder.IsAscendent
                                             ? filteredEmotes.OrderBy(emote => emote.GetName())
                                             : filteredEmotes.OrderByDescending(emote => emote.GetName()),
                                         _ => filteredEmotes,
                                     };

                    embeddedEmotes = filteredEmotes.ToList();
                    totalAmount += embeddedEmotes.Count;

                    // We always need to concat embedded emotes at the end, no matter the filter & sorting
                    // otherwise the pagination in the realm provider get inconsistent with the union of the embedded emotes
                    // The only way of getting to work properly is by the realm providing also off-chain emotes or request all emotes at once
                    // For example:
                    // 1. Set sort by name
                    // 2. Page 1 will contain some embedded emotes & owned emotes
                    // 3. Request page 2, the realm will not provide any of the owned emotes since they are part of page 1
                    // 4. We will probably skip most of the owned emotes in the grid becoming inconsistent
                    emotes = customOwnedEmotes.Concat(embeddedEmotes)
                                              .Skip((pageNumber - 1) * CURRENT_PAGE_SIZE)
                                              .Take(CURRENT_PAGE_SIZE)
                                              .ToArray();
                }

                if (emotes.Count == 0)
                {
                    view.NoSearchResults.SetActive(!string.IsNullOrEmpty(currentSearch));
                    view.NoCategoryResults.SetActive(!string.IsNullOrEmpty(currentCategory));
                    view.RegularResults.SetActive(string.IsNullOrEmpty(currentSearch) && string.IsNullOrEmpty(currentCategory));
                }
                else
                {
                    view.NoSearchResults.SetActive(false);
                    view.NoCategoryResults.SetActive(false);
                    view.RegularResults.SetActive(true);
                }

                SetGridElements(emotes);

                if (reconfigurePageSelector)
                    pageSelectorController.Configure(totalAmount, CURRENT_PAGE_SIZE);
            }

            RequestPageAsync(loadElementsCancellationToken!.Token).Forget();
        }

        private void SetGridAsLoading()
        {
            ClearPoolElements();

            for (var i = 0; i < CURRENT_PAGE_SIZE; i++)
            {
                BackpackEmoteGridItemView backpackItemView = gridItemsPool.Get();
                backpackItemView.LoadingView.StartLoadingAnimation(backpackItemView.FullBackpackItem);
                backpackItemView.gameObject.transform.SetAsLastSibling();
                loadingResults[i] = backpackItemView;
                usedPoolItems.Add(i.ToString(), backpackItemView);
            }
        }

        private void SetGridElements(IReadOnlyList<IEmote> emotes)
        {
            //Disables and sets the empty slots as first children to avoid the grid to be reorganized
            for (int j = emotes.Count; j < CURRENT_PAGE_SIZE; j++)
            {
                loadingResults[j]!.gameObject.transform.SetAsFirstSibling();
                loadingResults[j]!.LoadingView.gameObject.SetActive(false);
                loadingResults[j]!.FullBackpackItem.SetActive(false);
                usedPoolItems.Remove(j);

                if (loadingResults[j] != null)
                    gridItemsPool.Release(loadingResults[j]!);
            }

            for (int i = emotes.Count - 1; i >= 0; i--)
            {
                BackpackEmoteGridItemView backpackItemView = loadingResults[i]!;
                usedPoolItems.Remove(i);
                usedPoolItems.Add(emotes[i].GetUrn(), backpackItemView);
                backpackItemView.gameObject.transform.SetAsLastSibling();
                backpackItemView.OnSelectItem += SelectItem;
                backpackItemView.EquipButton.onClick.AddListener(() => commandBus.SendCommand(new BackpackEquipEmoteCommand(backpackItemView.ItemId)));
                backpackItemView.UnEquipButton.onClick.AddListener(() => commandBus.SendCommand(new BackpackUnEquipEmoteCommand(backpackItemView.ItemId)));
                backpackItemView.ItemId = emotes[i].GetUrn();
                backpackItemView.RarityBackground.sprite = rarityBackgrounds.GetTypeImage(emotes[i].GetRarity());
                backpackItemView.FlapBackground.color = rarityColors.GetColor(emotes[i].GetRarity());
                backpackItemView.CategoryImage.sprite = categoryIcons.GetTypeImage(EMOTE_CATEGORY);

                int equippedSlot = equippedEmotes.SlotOf(emotes[i]);
                bool isEquipped = equippedSlot != -1;
                backpackItemView.EquippedIcon.SetActive(isEquipped);
                backpackItemView.IsEquipped = isEquipped;
                backpackItemView.EquippedSlotLabel.gameObject.SetActive(isEquipped);
                backpackItemView.EquippedSlotLabel.text = equippedSlot.ToString();

                backpackItemView.SetEquipButtonsState();
                WaitForThumbnailAsync(emotes[i], backpackItemView, loadElementsCancellationToken!.Token).Forget();
            }
        }

        private void OnFilterCategory(string category)
        {
            currentCategory = string.IsNullOrEmpty(category) ? null : category;
            RequestAndFillEmotes(1, true);
        }

        private void OnSearch(string searchText)
        {
            currentSearch = string.IsNullOrEmpty(searchText) ? null : searchText;
            RequestAndFillEmotes(1, true);
        }

        private void OnSortChanged(BackpackGridSort sort)
        {
            string by = sort.OrderByOperation.ToString().ToLower();
            currentOrder = new IEmoteProvider.OrderOperation(by, sort.SortAscending);
            RequestAndFillEmotes(1, true);
        }

        private void OnCollectiblesOnlyChanged(bool collectiblesOnly)
        {
            onChainEmotesOnly = collectiblesOnly;
            RequestAndFillEmotes(1, true);
        }

        private async UniTaskVoid WaitForThumbnailAsync(IAvatarAttachment emote, BackpackItemView itemView, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            do { await UniTask.Delay(250, cancellationToken: ct); }
            while (emote.ThumbnailAssetResult == null || !emote.ThumbnailAssetResult.HasValue);

            itemView.WearableThumbnail.sprite = emote.ThumbnailAssetResult.Value.Asset;
            itemView.LoadingView.FinishLoadingAnimation(itemView.FullBackpackItem);
        }

        private void ClearPoolElements()
        {
            foreach (KeyValuePair<URN, BackpackEmoteGridItemView> backpackItemView in usedPoolItems)
            {
                backpackItemView.Value.EquipButton.onClick.RemoveAllListeners();
                backpackItemView.Value.UnEquipButton.onClick.RemoveAllListeners();
                backpackItemView.Value.OnSelectItem -= SelectItem;
                backpackItemView.Value.EquippedIcon.SetActive(false);
                backpackItemView.Value.IsEquipped = false;
                backpackItemView.Value.ItemId = "";
                gridItemsPool.Release(backpackItemView.Value);
            }

            for (var i = 0; i < loadingResults.Length; i++)
                loadingResults[i] = null;

            usedPoolItems.Clear();
        }

        private void SelectItem(string itemId) =>
            commandBus.SendCommand(new BackpackSelectEmoteCommand(itemId));

        private void OnUnequip(int slot, IEmote? emote)
        {
            if (emote == null) return;
            if (!usedPoolItems.TryGetValue(emote.GetUrn(), out BackpackEmoteGridItemView backpackItemView)) return;
            backpackItemView.EquippedIcon.SetActive(false);
            backpackItemView.IsEquipped = false;
            backpackItemView.SetEquipButtonsState();
            backpackItemView.EquippedSlotLabel.gameObject.SetActive(false);
        }

        private void OnEquip(int slot, IEmote emote)
        {
            if (!usedPoolItems.TryGetValue(emote.GetUrn(), out BackpackEmoteGridItemView backpackItemView)) return;
            backpackItemView.IsEquipped = true;
            backpackItemView.SetEquipButtonsState();
            backpackItemView.EquippedSlotLabel.gameObject.SetActive(true);
            backpackItemView.EquippedSlotLabel.text = slot.ToString();
        }

        private void OnWearableEquipped(IWearable wearable)
        {
            if (wearable.GetCategory() != WearablesConstants.Categories.BODY_SHAPE) return;

            foreach (BodyShape bodyShape in BodyShape.VALUES)
            {
                if (wearable.GetUrn() != bodyShape) continue;
                currentBodyShape = bodyShape;
                return;
            }
        }
    }
}
