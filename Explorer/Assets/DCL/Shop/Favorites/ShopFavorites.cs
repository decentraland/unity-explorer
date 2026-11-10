using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Shop
{
    /// <summary>
    ///     The signed-in account's favorite items, shared with the web shop through the marketplace picks service.
    ///     A toggle applies at once and is sent afterwards; a rejected toggle is undone by reading the list again.
    /// </summary>
    public sealed class ShopFavorites : IDisposable
    {
        private readonly MarketplaceShopAPIClient api;
        private readonly IWeb3IdentityCache identityCache;
        private readonly List<string> ids = new ();
        private readonly HashSet<string> idSet = new (StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> sending = new (StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RecentToggle> recentToggles = new (StringComparer.OrdinalIgnoreCase);
        private readonly List<string> staleTogglesScratch = new ();

        private CancellationTokenSource lifeCts = new ();
        private CancellationTokenSource? loadCts;
        private int toggleSequence;

        /// <summary>Favorite item keys, newest first.</summary>
        public IReadOnlyList<string> Ids => ids;

        public int Count => ids.Count;

        /// <summary>A toggle was applied locally: key, the toggled item, whether it is now a favorite.</summary>
        public event Action<string, ShopItemCardModel, bool>? Changed;

        /// <summary>The list was read from the server; false when the read failed and the previous list was kept.</summary>
        public event Action<bool>? Loaded;

        /// <summary>The user's toggle with the resulting number of favorites; raised before the server answers.</summary>
        public event Action<ShopItemCardModel, bool, ShopCartSource, int>? Toggled;

        public event Action? ToggleFailed;

        public ShopFavorites(MarketplaceShopAPIClient api, IWeb3IdentityCache identityCache)
        {
            this.api = api;
            this.identityCache = identityCache;
            identityCache.OnIdentityChanged += OnIdentityChanged;
            identityCache.OnIdentityCleared += OnIdentityChanged;
        }

        public void Dispose()
        {
            identityCache.OnIdentityChanged -= OnIdentityChanged;
            identityCache.OnIdentityCleared -= OnIdentityChanged;
            loadCts.SafeCancelAndDispose();
            lifeCts.SafeCancelAndDispose();
        }

        /// <summary>
        ///     The key the picks service knows the item by; null for rows without an item id (per-token listings),
        ///     which cannot be favorited.
        /// </summary>
        public static string? KeyOf(ShopItemCardModel model) =>
            string.IsNullOrEmpty(model.ItemId) ? null : $"{model.ContractAddress}-{model.ItemId}";

        public bool IsFavorite(string key) =>
            idSet.Contains(key);

        /// <summary>Reads the list again; the newest call replaces one still in flight.</summary>
        public void Reload()
        {
            loadCts = loadCts.SafeRestartLinked(lifeCts.Token);
            LoadAsync(loadCts.Token).Forget();
        }

        public void Toggle(ShopItemCardModel model, ShopCartSource source)
        {
            string? key = KeyOf(model);

            if (key == null)
                return;

            bool favorite = !idSet.Contains(key);
            Apply(key, favorite);
            recentToggles[key] = new RecentToggle(favorite, ++toggleSequence);

            Changed?.Invoke(key, model, favorite);
            Toggled?.Invoke(model, favorite, source, ids.Count);

            bool isAlreadySending = sending.ContainsKey(key);
            sending[key] = favorite;

            if (!isAlreadySending)
                SendAsync(key, lifeCts.Token).Forget();
        }

        private async UniTaskVoid LoadAsync(CancellationToken ct)
        {
            // The reply describes the list as it was when the request left, so toggles made from here on are newer than it
            int sequenceAtStart = toggleSequence;
            Result<List<string>> result = await api.GetFavoriteIdsAsync(ct).SuppressToResultAsync(ReportCategory.UI);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                Loaded?.Invoke(false);
                return;
            }

            ids.Clear();
            idSet.Clear();

            foreach (string id in result.Value)
            {
                if (idSet.Add(id))
                    ids.Add(id);
            }

            staleTogglesScratch.Clear();

            foreach (KeyValuePair<string, RecentToggle> toggle in recentToggles)
            {
                // A toggle still being sent may not be in the reply yet either.
                if (toggle.Value.Sequence > sequenceAtStart || sending.ContainsKey(toggle.Key))
                    Apply(toggle.Key, toggle.Value.Favorite);
                else
                    staleTogglesScratch.Add(toggle.Key);
            }

            foreach (string key in staleTogglesScratch)
                recentToggles.Remove(key);

            Loaded?.Invoke(true);
        }

        /// <summary>
        ///     Sends the state the user left the item in; a click made while a request is in flight is sent as a
        ///     follow-up, so the loop repeats only once per such click.
        /// </summary>
        private async UniTaskVoid SendAsync(string key, CancellationToken ct)
        {
            bool sent;

            do
            {
                sent = sending[key];
                EnumResult<TaskError> result = await api.SetFavoriteAsync(key, sent, ct).SuppressToResultAsync(ReportCategory.UI);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                {
                    sending.Remove(key);
                    recentToggles.Remove(key);
                    ToggleFailed?.Invoke();
                    Reload();
                    return;
                }
            }
            while (sending[key] != sent);

            sending.Remove(key);
        }

        private void Apply(string key, bool favorite)
        {
            if (favorite)
            {
                if (idSet.Add(key))
                    ids.Insert(0, key);
            }
            else if (idSet.Remove(key))
                ids.RemoveAll(id => string.Equals(id, key, StringComparison.OrdinalIgnoreCase));
        }

        // Favorites belong to the account: nothing read or sent for the previous one may reach the next
        private void OnIdentityChanged()
        {
            lifeCts = lifeCts.SafeRestart();
            ids.Clear();
            idSet.Clear();
            sending.Clear();
            recentToggles.Clear();
        }

        private readonly struct RecentToggle
        {
            public readonly bool Favorite;
            public readonly int Sequence;

            public RecentToggle(bool favorite, int sequence)
            {
                Favorite = favorite;
                Sequence = sequence;
            }
        }
    }
}
