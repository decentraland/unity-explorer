using DCL.Web3.Identities;
using System;
using System.Collections.Generic;

namespace DCL.MarketplaceCredits.Purchase.Cart
{
    /// <summary>
    ///     The in-memory shopping cart, shared by the shop UI (adds) and the checkout (removes what settled). It
    ///     belongs to the signed-in account: a different address signing in starts from an empty cart, signing out
    ///     keeps it.
    /// </summary>
    public sealed class ShopCart : IDisposable
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly List<ShopCartLine> lines = new ();
        private readonly Dictionary<string, ShopCartLine> linesById = new (StringComparer.OrdinalIgnoreCase);
        private readonly List<ShopCartLine> removedScratch = new ();

        private string? owner;

        public IReadOnlyList<ShopCartLine> Lines => lines;

        public int Count => lines.Count;

        public int TotalUnits { get; private set; }

        public long TotalCredits { get; private set; }

        public event Action? Changed;

        /// <summary>A new line, or one more unit of a primary line; never raised for a no-op add.</summary>
        public event Action<ShopCartLine, ShopCartSource>? ItemAdded;

        /// <summary>An explicit removal; the identity wipe is silent.</summary>
        public event Action<ShopCartLine>? ItemRemoved;

        public ShopCart(IWeb3IdentityCache identityCache)
        {
            this.identityCache = identityCache;
            identityCache.OnIdentityChanged += OnIdentityChanged;
            identityCache.OnIdentityCleared += OnIdentityChanged;
        }

        public void Dispose()
        {
            identityCache.OnIdentityChanged -= OnIdentityChanged;
            identityCache.OnIdentityCleared -= OnIdentityChanged;
        }

        public bool Contains(string lineId) =>
            linesById.ContainsKey(lineId);

        public bool Contains(ShopListingDto listing) =>
            linesById.ContainsKey(listing.CartLineId());

        /// <summary>Whether the primary line of an item is in the cart, for rows that were never resolved to a listing.</summary>
        public bool Contains(string contractAddress, string itemId) =>
            linesById.ContainsKey(ShopListingDtoExtensions.CartLineId(contractAddress, itemId, null));

        public bool TryGet(string lineId, out ShopCartLine? line) =>
            linesById.TryGetValue(lineId, out line);

        /// <summary>
        ///     Adds one unit. An existing primary line is bumped up to its stock, an existing secondary line is left
        ///     alone; both keep their original source. Returns false when nothing changed.
        /// </summary>
        public bool Add(ShopListingDto listing, ShopCartSource source, string? outfitId = null, string? creatorName = null)
        {
            AdoptOwnerIfUnclaimed();

            string id = listing.CartLineId();

            if (linesById.TryGetValue(id, out ShopCartLine? existing))
            {
                if (!existing.IsPrimary || existing.Quantity >= existing.StockCap)
                    return false;

                existing.Quantity++;
                RecomputeTotals();
                ItemAdded?.Invoke(existing, source);
                Changed?.Invoke();
                return true;
            }

            var line = new ShopCartLine(listing, source, outfitId, creatorName);
            lines.Add(line);
            linesById[id] = line;
            RecomputeTotals();
            ItemAdded?.Invoke(line, source);
            Changed?.Invoke();
            return true;
        }

        public bool Remove(string lineId)
        {
            if (!linesById.Remove(lineId, out ShopCartLine? line))
                return false;

            lines.Remove(line);
            RecomputeTotals();
            ItemRemoved?.Invoke(line);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        ///     Takes one unit off its line per bought unit and removes a line that reaches zero, so units added to a
        ///     line after it was reviewed stay in the cart. Raises ItemRemoved per removed line and Changed once.
        /// </summary>
        public void RemoveBought(IReadOnlyList<ReviewedCartLine> boughtUnits)
        {
            removedScratch.Clear();
            var changed = false;

            foreach (ReviewedCartLine unit in boughtUnits)
            {
                if (!linesById.TryGetValue(unit.Line.Id, out ShopCartLine? line))
                    continue;

                changed = true;

                if (line.Quantity > 1)
                {
                    line.Quantity--;
                    continue;
                }

                linesById.Remove(line.Id);
                lines.Remove(line);
                removedScratch.Add(line);
            }

            if (!changed)
                return;

            RecomputeTotals();

            foreach (ShopCartLine line in removedScratch)
                ItemRemoved?.Invoke(line);

            removedScratch.Clear();
            Changed?.Invoke();
        }

        /// <summary>Primary lines only; clamped to 1..StockCap. Removal is a separate, explicit action.</summary>
        public bool SetQuantity(string lineId, int quantity)
        {
            if (!linesById.TryGetValue(lineId, out ShopCartLine? line) || !line.IsPrimary)
                return false;

            int clamped = Math.Clamp(quantity, 1, line.StockCap);

            if (clamped == line.Quantity)
                return false;

            line.Quantity = clamped;
            RecomputeTotals();
            Changed?.Invoke();
            return true;
        }

        public bool Increment(string lineId) =>
            linesById.TryGetValue(lineId, out ShopCartLine? line) && SetQuantity(lineId, line.Quantity + 1);

        public bool Decrement(string lineId) =>
            linesById.TryGetValue(lineId, out ShopCartLine? line) && SetQuantity(lineId, line.Quantity - 1);

        public void Clear()
        {
            if (lines.Count == 0)
                return;

            lines.Clear();
            linesById.Clear();
            RecomputeTotals();
            Changed?.Invoke();
        }

        private void RecomputeTotals()
        {
            var units = 0;
            var credits = 0L;

            foreach (ShopCartLine line in lines)
            {
                units += line.Quantity;
                credits += line.TotalCredits;
            }

            TotalUnits = units;
            TotalCredits = credits;
        }

        private void AdoptOwnerIfUnclaimed()
        {
            if (owner != null)
                return;

            IWeb3Identity? identity = identityCache.Identity;

            if (identity != null)
                owner = ((string)identity.Address).ToLowerInvariant();
        }

        // Web shop policy: an unclaimed cart is adopted, signing out keeps the cart, a different account starts empty.
        private void OnIdentityChanged()
        {
            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return;

            string address = ((string)identity.Address).ToLowerInvariant();

            if (owner == null || string.Equals(owner, address, StringComparison.OrdinalIgnoreCase))
            {
                owner = address;
                return;
            }

            owner = address;
            Clear();
        }
    }
}
