using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Shop
{
    public class ShopOverviewView : MonoBehaviour
    {
        private const int ITEM_CARDS_POOL_CAPACITY = 24;
        private const int OUTFIT_CARDS_POOL_CAPACITY = 12;
        private const int OUTFITS_PER_PAGE = 4;
        private const int OUTFITS_PER_PAGE_COMPACT = 3;
        private const int ITEMS_PER_PAGE = 6;
        private const int ITEMS_PER_PAGE_COMPACT = 4;

        [field: SerializeField] public ScrollRect Scroll { get; private set; } = null!;
        [field: SerializeField] public ShopCarouselView TrendingCarousel { get; private set; } = null!;
        [field: SerializeField] public ShopCarouselView OutfitsRow { get; private set; } = null!;
        [field: SerializeField] public ShopCarouselView NewCreationsCarousel { get; private set; } = null!;
        [field: SerializeField] public ShopItemCardView ItemCardPrefab { get; private set; } = null!;
        [field: SerializeField] public ShopOutfitCardView OutfitCardPrefab { get; private set; } = null!;
        [field: SerializeField] public GameObject EmptyContainer { get; private set; } = null!;
        [field: SerializeField] public Button EmptyBrowseButton { get; private set; } = null!;

        private readonly List<ShopItemCardView> activeItemCards = new ();
        private readonly List<ShopOutfitCardView> activeOutfitCards = new ();
        private IObjectPool<ShopItemCardView> itemCardsPool = null!;
        private IObjectPool<ShopOutfitCardView> outfitCardsPool = null!;

        public IReadOnlyList<ShopItemCardView> ActiveItemCards => activeItemCards;

        public IReadOnlyList<ShopOutfitCardView> ActiveOutfitCards => activeOutfitCards;

        public event Action<ShopItemCardView>? ItemAddToCartClicked;
        public event Action<ShopItemCardView>? ItemBuyClicked;
        public event Action<ShopItemCardView>? ItemClicked;
        public event Action<ShopOutfitCardView>? OutfitAddClicked;
        public event Action? ViewAllClicked;

        private void Awake()
        {
            itemCardsPool = new ObjectPool<ShopItemCardView>(CreateItemCard, defaultCapacity: ITEM_CARDS_POOL_CAPACITY,
                actionOnGet: card => card.gameObject.SetActive(true),
                actionOnRelease: card =>
                {
                    card.gameObject.SetActive(false);
                    card.transform.SetParent(transform, false);
                });

            outfitCardsPool = new ObjectPool<ShopOutfitCardView>(CreateOutfitCard, defaultCapacity: OUTFIT_CARDS_POOL_CAPACITY,
                actionOnGet: card => card.gameObject.SetActive(true),
                actionOnRelease: card =>
                {
                    card.gameObject.SetActive(false);
                    card.transform.SetParent(transform, false);
                });

            TrendingCarousel.ViewAllClicked += OnViewAllClicked;
            NewCreationsCarousel.ViewAllClicked += OnViewAllClicked;
            EmptyBrowseButton.onClick.AddListener(OnViewAllClicked);
            SetCompact(false);
        }

        private void OnDestroy()
        {
            TrendingCarousel.ViewAllClicked -= OnViewAllClicked;
            NewCreationsCarousel.ViewAllClicked -= OnViewAllClicked;
            EmptyBrowseButton.onClick.RemoveListener(OnViewAllClicked);
        }

        public ShopItemCardView RentItemCard(ShopCarouselView row)
        {
            ShopItemCardView card = itemCardsPool.Get();
            card.transform.SetParent(row.Track, false);
            card.transform.SetAsLastSibling();
            activeItemCards.Add(card);
            return card;
        }

        public ShopOutfitCardView RentOutfitCard()
        {
            ShopOutfitCardView card = outfitCardsPool.Get();
            card.transform.SetParent(OutfitsRow.Track, false);
            card.transform.SetAsLastSibling();
            activeOutfitCards.Add(card);
            return card;
        }

        public void ReleaseRowCards(ShopCarouselView row)
        {
            for (int i = activeItemCards.Count - 1; i >= 0; i--)
            {
                if (activeItemCards[i].transform.parent != row.Track)
                    continue;

                itemCardsPool.Release(activeItemCards[i]);
                activeItemCards.RemoveAt(i);
            }
        }

        public void ReleaseOutfitCards()
        {
            foreach (ShopOutfitCardView card in activeOutfitCards)
                outfitCardsPool.Release(card);

            activeOutfitCards.Clear();
        }

        public void ReleaseAllCards()
        {
            foreach (ShopItemCardView card in activeItemCards)
                itemCardsPool.Release(card);

            activeItemCards.Clear();
            ReleaseOutfitCards();
        }

        /// <summary>
        ///     Fewer cards per page while the item panel takes the right side of the section.
        /// </summary>
        public void SetCompact(bool compact)
        {
            Vector2 outfitSize = ((RectTransform)OutfitCardPrefab.transform).sizeDelta;
            Vector2 itemSize = ((RectTransform)ItemCardPrefab.transform).sizeDelta;
            OutfitsRow.Configure(outfitSize.x, outfitSize.y, compact ? OUTFITS_PER_PAGE_COMPACT : OUTFITS_PER_PAGE);
            TrendingCarousel.Configure(itemSize.x, itemSize.y, compact ? ITEMS_PER_PAGE_COMPACT : ITEMS_PER_PAGE);
            NewCreationsCarousel.Configure(itemSize.x, itemSize.y, compact ? ITEMS_PER_PAGE_COMPACT : ITEMS_PER_PAGE);
        }

        public void SetEmptyVisible(bool visible) =>
            EmptyContainer.SetActive(visible);

        public void ResetScroll() =>
            Scroll.verticalNormalizedPosition = 1f;

        private ShopItemCardView CreateItemCard()
        {
            ShopItemCardView card = Instantiate(ItemCardPrefab, transform);
            card.AddToCartClicked = c => ItemAddToCartClicked?.Invoke(c);
            card.BuyClicked = c => ItemBuyClicked?.Invoke(c);
            card.Clicked = c => ItemClicked?.Invoke(c);
            return card;
        }

        private ShopOutfitCardView CreateOutfitCard()
        {
            ShopOutfitCardView card = Instantiate(OutfitCardPrefab, transform);
            card.AddClicked = c => OutfitAddClicked?.Invoke(c);
            return card;
        }

        private void OnViewAllClicked() =>
            ViewAllClicked?.Invoke();
    }
}
