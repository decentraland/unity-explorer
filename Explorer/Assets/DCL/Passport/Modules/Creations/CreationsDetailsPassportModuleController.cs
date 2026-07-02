using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Fields;
using DCL.Profiles;
using DCL.UI;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Passport.Modules.Creations
{
    public class CreationsDetailsPassportModuleController : IPassportModuleController
    {
        private const int ITEMS_POOL_DEFAULT_CAPACITY = 8;
        private const string WEARABLE_CATEGORY = "wearable";
        private const string EMOTE_CATEGORY = "emote";
        private const string EMOTE_CATEGORY_ICON = "emote";

        private readonly CreationsDetailsPassportModuleView view;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly IWebBrowser webBrowser;
        private readonly ImageControllerProvider imageControllerProvider;
        private readonly PassportErrorsController passportErrorsController;
        private readonly IObjectPool<EquippedItem_PassportFieldView> wearablesItemsPool;
        private readonly IObjectPool<EquippedItem_PassportFieldView> emotesItemsPool;
        private readonly List<EquippedItem_PassportFieldView> instantiatedWearables = new ();
        private readonly List<EquippedItem_PassportFieldView> instantiatedEmotes = new ();
        private readonly List<Texture2DRef> loadedThumbnails = new ();

        private Profile? currentProfile;
        private CancellationTokenSource? loadCreationsCts;

        public CreationsDetailsPassportModuleController(
            CreationsDetailsPassportModuleView view,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            IWebBrowser webBrowser,
            ImageControllerProvider imageControllerProvider,
            PassportErrorsController passportErrorsController)
        {
            this.view = view;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.webBrowser = webBrowser;
            this.imageControllerProvider = imageControllerProvider;
            this.passportErrorsController = passportErrorsController;

            wearablesItemsPool = CreateItemsPool(view.CreatedWearablesContainer);
            emotesItemsPool = CreateItemsPool(view.CreatedEmotesContainer);
        }

        private IObjectPool<EquippedItem_PassportFieldView> CreateItemsPool(RectTransform parent) =>
            new ObjectPool<EquippedItem_PassportFieldView>(
                () => InstantiateItemPrefab(parent),
                defaultCapacity: ITEMS_POOL_DEFAULT_CAPACITY,
                actionOnGet: itemView =>
                {
                    itemView.gameObject.SetActive(true);
                    itemView.gameObject.transform.SetAsLastSibling();
                    itemView.SetAsLoading(true);
                },
                actionOnRelease: itemView =>
                {
                    itemView.SetAsLoading(false);
                    itemView.BuyButton.onClick.RemoveAllListeners();
                    itemView.gameObject.SetActive(false);
                }
            );

        private EquippedItem_PassportFieldView InstantiateItemPrefab(RectTransform parent) =>
            Object.Instantiate(view.EquippedItemPrefab, parent);

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            Clear();

            loadCreationsCts = loadCreationsCts.SafeRestart();

            view.MainLoadingSpinner.SetActive(true);
            view.NoCreationsLabel.SetActive(false);
            view.NoWearablesLabel.SetActive(false);
            view.NoEmotesLabel.SetActive(false);
            view.WearablesLabel.SetActive(false);
            view.EmotesLabel.SetActive(false);

            LoadCreationsAsync(loadCreationsCts.Token).Forget();
        }

        public void Clear()
        {
            loadCreationsCts.SafeCancelAndDispose();

            ClearItems(wearablesItemsPool, instantiatedWearables);
            ClearItems(emotesItemsPool, instantiatedEmotes);

            foreach (Texture2DRef thumbnail in loadedThumbnails)
                thumbnail.Dispose();

            loadedThumbnails.Clear();
        }

        public void Dispose() =>
            Clear();

        private async UniTaskVoid LoadCreationsAsync(CancellationToken ct)
        {
            try
            {
                (int wearablesCount, int emotesCount) = await UniTask.WhenAll(
                    LoadCategoryAsync(WEARABLE_CATEGORY, wearablesItemsPool, instantiatedWearables, false, ct),
                    LoadCategoryAsync(EMOTE_CATEGORY, emotesItemsPool, instantiatedEmotes, true, ct));

                SetupUiElements(wearablesCount, emotesCount);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                view.MainLoadingSpinner.SetActive(false);
                const string ERROR_MESSAGE = "There was an error while loading the creations. Please try again!";
                passportErrorsController.Show(ERROR_MESSAGE);
                ReportHub.LogError(ReportCategory.UI, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }

        private void SetupUiElements(int wearablesCount, int emotesCount)
        {
            view.MainLoadingSpinner.SetActive(false);

            bool hasWearables = wearablesCount > 0;
            bool hasEmotes = emotesCount > 0;
            bool hasAnyCreation = hasWearables || hasEmotes;

            view.NoCreationsLabel.SetActive(!hasAnyCreation);
            view.CreatedWearablesContainer.gameObject.SetActive(hasWearables);
            view.CreatedEmotesContainer.gameObject.SetActive(hasEmotes);
            view.NoWearablesLabel.SetActive(hasAnyCreation && !hasWearables);
            view.WearablesLabel.SetActive(hasAnyCreation);
            view.NoEmotesLabel.SetActive(hasAnyCreation && !hasEmotes);
            view.EmotesLabel.SetActive(hasAnyCreation);
        }

        private async UniTask<int> LoadCategoryAsync(
            string category,
            IObjectPool<EquippedItem_PassportFieldView> pool,
            List<EquippedItem_PassportFieldView> instantiatedItems,
            bool isEmote,
            CancellationToken ct)
        {
            string baseUrl = decentralandUrlsSource.Url(DecentralandUrl.MarketplaceApiLink);
            var url = URLAddress.FromString($"{baseUrl}?category={category}&creator={currentProfile?.UserId}&includeSocialEmotes=false");

            MarketplaceCatalogResponse response = await webRequestController.GetAsync(url, ct, ReportCategory.UI)
                                                                            .CreateFromJson<MarketplaceCatalogResponse>(WRJsonParser.Unity);

            if (response?.data == null)
                return 0;

            foreach (MarketplaceCatalogItem item in response.data)
            {
                EquippedItem_PassportFieldView itemView = pool.Get();
                SetupItemView(itemView, item, isEmote, ct);
                instantiatedItems.Add(itemView);
            }

            return response.data.Length;
        }

        private void SetupItemView(EquippedItem_PassportFieldView itemView, MarketplaceCatalogItem item, bool isEmote, CancellationToken ct)
        {
            Sprite raritySprite = rarityBackgrounds.GetTypeImage(item.rarity);
            Color rarityColor = rarityColors.GetColor(item.rarity);

            itemView.AssetNameText.text = item.name;
            itemView.ItemId = item.urn ?? string.Empty;
            itemView.RarityBackground.sprite = raritySprite;
            itemView.RarityLabelText.text = item.rarity;
            itemView.RarityLabelText.color = rarityColor;
            itemView.RarityBackground2.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, itemView.RarityBackground2.color.a);
            itemView.FlapBackground.color = rarityColor;
            itemView.CategoryImage.sprite = categoryIcons.GetTypeImage(isEmote ? EMOTE_CATEGORY_ICON : item.data?.wearable?.category);

            string marketplaceLink = GetMarketplaceLink(item);
            itemView.BuyButton.gameObject.SetActive(item.isOnSale && marketplaceLink != string.Empty);
            itemView.BuyButton.onClick.RemoveAllListeners();
            itemView.BuyButton.onClick.AddListener(() => webBrowser.OpenUrl(marketplaceLink));

            itemView.SetAsLoading(false);
            WaitForThumbnailAsync(item.thumbnail, itemView, ct).Forget();
        }

        private string GetMarketplaceLink(MarketplaceCatalogItem item)
        {
            if (string.IsNullOrEmpty(item.url))
                return string.Empty;

            return $"{decentralandUrlsSource.Url(DecentralandUrl.Market)}{item.url}";
        }

        private async UniTaskVoid WaitForThumbnailAsync(string? thumbnailUrl, EquippedItem_PassportFieldView itemView, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(thumbnailUrl))
                return;

            try
            {
                Texture2DRef? textureRef = await imageControllerProvider.LoadTextureAsync(thumbnailUrl, ct);

                if (!textureRef.HasValue || ct.IsCancellationRequested)
                {
                    textureRef?.Dispose();
                    return;
                }

                loadedThumbnails.Add(textureRef.Value);
                Texture2D texture = textureRef.Value.Texture;
                itemView.EquippedItemThumbnail.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                itemView.EquippedItemThumbnail.sprite = null;
                ReportHub.LogError(ReportCategory.UI, $"There was an error while trying to load a creation thumbnail. ERROR: {e.Message}");
            }
        }

        private static void ClearItems(IObjectPool<EquippedItem_PassportFieldView> pool, List<EquippedItem_PassportFieldView> instantiatedItems)
        {
            foreach (EquippedItem_PassportFieldView item in instantiatedItems)
                pool.Release(item);

            instantiatedItems.Clear();
        }
    }
}
