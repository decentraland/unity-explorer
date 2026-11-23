using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Diagnostics;
using DCL.UI;
using MVC;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    /// <summary>
    ///     The cart popup: the lines with their quantities and the live total. Checkout closes it and hands over
    ///     to the checkout popup.
    /// </summary>
    public class ShopCartModalController : ControllerBase<ShopCartModalView, ShopCartModalParams>
    {
        private const float ANCHOR_GAP = 8f;
        private const string TITLE_FORMAT = "Your Cart ({0})";

        private readonly ShopCart cart;
        private readonly ISpriteCache spriteCache;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly Func<CancellationToken, UniTask> openCheckoutAsync;
        private readonly List<ShopCartLineView> activeLines = new ();
        private readonly CancellationTokenSource disposalCts = new ();

        private ObjectPool<ShopCartLineView>? linePool;
        private CancellationTokenSource? thumbnailsCts;
        private IShopCartFavorites? favorites;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<string, int, long>? CartOpened;
        public event Action<ShopCartLine>? LineRemoved;

        public ShopCartModalController(
            ViewFactoryMethod viewFactory,
            ShopCart cart,
            ISpriteCache spriteCache,
            NftTypeIconSO rarityBackgrounds,
            Func<CancellationToken, UniTask> openCheckoutAsync)
            : base(viewFactory)
        {
            this.cart = cart;
            this.spriteCache = spriteCache;
            this.rarityBackgrounds = rarityBackgrounds;
            this.openCheckoutAsync = openCheckoutAsync;
        }

        public override void Dispose()
        {
            UnsubscribeFromCart();
            thumbnailsCts.SafeCancelAndDispose();
            disposalCts.SafeCancelAndDispose();

            if (viewInstance != null)
                viewInstance.CheckoutButton.onClick.RemoveListener(OnCheckoutClicked);

            linePool?.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            if (viewInstance == null)
                return;

            ShopCartModalView view = viewInstance;

            linePool = new ObjectPool<ShopCartLineView>(
                () => CreateLineView(view),
                actionOnGet: line => line.gameObject.SetActive(true),
                actionOnRelease: line =>
                {
                    line.Unbind();
                    line.gameObject.SetActive(false);
                });

            view.CheckoutButton.onClick.AddListener(OnCheckoutClicked);
        }

        protected override void OnBeforeViewShow() =>
            viewInstance?.PositionBelow(inputData.AnchorBottomRight, ANCHOR_GAP);

        protected override void OnViewShow()
        {
            cart.Changed += RenderLines;
            favorites = inputData.Favorites;
            favorites.FavoritesChanged += OnFavoritesChanged;

            CartOpened?.Invoke(inputData.Source, cart.TotalUnits, cart.TotalCredits);

            RenderLines();
        }

        protected override void OnViewClose()
        {
            UnsubscribeFromCart();
            thumbnailsCts.SafeCancelAndDispose();
            thumbnailsCts = null;
            ReleaseLines();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return;

            await UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CloseBackground.OnClickAsync(ct));
        }

        private void UnsubscribeFromCart()
        {
            cart.Changed -= RenderLines;

            if (favorites != null)
            {
                favorites.FavoritesChanged -= OnFavoritesChanged;
                favorites = null;
            }
        }

        private ShopCartLineView CreateLineView(ShopCartModalView view)
        {
            ShopCartLineView line = UnityEngine.Object.Instantiate(view.LinePrefab, view.LinesContainer);
            line.IncrementClicked = OnIncrementClicked;
            line.DecrementClicked = OnDecrementClicked;
            line.RemoveClicked = OnRemoveClicked;
            line.FavoriteClicked = OnFavoriteClicked;
            return line;
        }

        private void RenderLines()
        {
            if (viewInstance == null || linePool == null)
                return;

            thumbnailsCts = thumbnailsCts.SafeRestart();

            // Rows are rebound in place: a row that stays under the pointer keeps its hover buttons.
            for (var i = 0; i < cart.Lines.Count; i++)
            {
                ShopCartLine line = cart.Lines[i];
                ShopCartLineView lineView;

                if (i < activeLines.Count)
                    lineView = activeLines[i];
                else
                {
                    lineView = linePool.Get();
                    lineView.transform.SetAsLastSibling();
                    activeLines.Add(lineView);
                }

                Sprite? cached = spriteCache.GetCachedSprite(line.Listing.thumbnail);
                lineView.Bind(line, cached, rarityBackgrounds.GetTypeImage(line.Listing.rarity));
                RefreshFavorite(lineView, line);

                if (cached == null)
                    LoadThumbnailAsync(lineView, line.Listing.thumbnail, thumbnailsCts.Token).Forget();
            }

            for (int i = activeLines.Count - 1; i >= cart.Lines.Count; i--)
            {
                linePool.Release(activeLines[i]);
                activeLines.RemoveAt(i);
            }

            viewInstance.EmptyState.SetActive(cart.Count == 0);
            viewInstance.CheckoutButton.interactable = cart.Count > 0;
            viewInstance.TitleText.text = string.Format(TITLE_FORMAT, cart.TotalUnits);
            viewInstance.TotalCreditsText.text = cart.TotalCredits.ToString();
        }

        private void ReleaseLines()
        {
            if (linePool == null)
                return;

            foreach (ShopCartLineView line in activeLines)
                linePool.Release(line);

            activeLines.Clear();
        }

        private async UniTaskVoid LoadThumbnailAsync(ShopCartLineView lineView, string url, CancellationToken ct)
        {
            string? boundId = lineView.BoundLineId;

            try
            {
                Sprite? sprite = await spriteCache.GetSpriteAsync(url, ct: ct);

                if (!ct.IsCancellationRequested && sprite != null && lineView.BoundLineId == boundId)
                    lineView.SetThumbnail(sprite);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Cart thumbnail failed for {url}: {e.Message}");
            }
        }

        private void OnIncrementClicked(ShopCartLineView lineView)
        {
            if (lineView.BoundLineId != null)
                cart.Increment(lineView.BoundLineId);
        }

        private void OnDecrementClicked(ShopCartLineView lineView)
        {
            if (lineView.BoundLineId != null)
                cart.Decrement(lineView.BoundLineId);
        }

        private void OnRemoveClicked(ShopCartLineView lineView)
        {
            if (!TryGetBoundLine(lineView, out ShopCartLine? line))
                return;

            LineRemoved?.Invoke(line);
            cart.Remove(line.Id);
        }

        private void OnFavoriteClicked(ShopCartLineView lineView)
        {
            if (TryGetBoundLine(lineView, out ShopCartLine? line))
                inputData.Favorites.Toggle(line.Listing, line.Source);
        }

        private void OnFavoritesChanged()
        {
            foreach (ShopCartLineView lineView in activeLines)
            {
                if (TryGetBoundLine(lineView, out ShopCartLine? line))
                    RefreshFavorite(lineView, line);
            }
        }

        private bool TryGetBoundLine(ShopCartLineView lineView, [NotNullWhen(true)] out ShopCartLine? line)
        {
            line = null;
            return lineView.BoundLineId != null && cart.TryGet(lineView.BoundLineId, out line) && line != null;
        }

        private void RefreshFavorite(ShopCartLineView lineView, ShopCartLine line) =>
            lineView.SetFavorite(inputData.Favorites.CanBeFavorite(line.Listing), inputData.Favorites.IsFavorite(line.Listing));

        private void OnCheckoutClicked()
        {
            if (cart.Count == 0)
                return;

            RequestClose();
            OpenCheckoutAfterCloseAsync(disposalCts.Token).Forget();
        }

        private async UniTaskVoid OpenCheckoutAfterCloseAsync(CancellationToken ct)
        {
            try { await openCheckoutAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private void RequestClose() =>
            viewInstance?.CloseButton.onClick.Invoke();
    }
}
