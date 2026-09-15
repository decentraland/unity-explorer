using System;
using System.Collections.Generic;

namespace DCL.MarketplaceCredits.Purchase.Cart
{
    public enum CartLineIssue
    {
        /// <summary>No live listing, sold out, or a price that could not be read: never charged off the snapshot.</summary>
        Unavailable,

        /// <summary>The buyer's own listing.</summary>
        OwnListing,
    }

    /// <summary>A cart line against its LIVE listing: the quote is per unit and is what gets authorized.</summary>
    public readonly struct ReviewedCartLine
    {
        public readonly ShopCartLine Line;
        public readonly CreditsPurchaseQuote Quote;
        public readonly int Quantity;

        public bool PriceChanged => Quote.Credits != Line.Listing.priceCredits;
        public int UnitCredits => Quote.Credits;
        public int UnitUsdCents => Quote.UsdCents;
        public int TotalCredits => Quote.Credits * Quantity;

        public ReviewedCartLine(ShopCartLine line, in CreditsPurchaseQuote quote, int quantity)
        {
            Line = line;
            Quote = quote;
            Quantity = quantity;
        }
    }

    public readonly struct CartReviewIssue
    {
        public readonly ShopCartLine Line;
        public readonly CartLineIssue Issue;

        public CartReviewIssue(ShopCartLine line, CartLineIssue issue)
        {
            Line = line;
            Issue = issue;
        }
    }

    /// <summary>
    ///     Every cart line classified against its live listing, plus the live total. OrderChanged asks the buyer
    ///     to confirm again because a row was dropped or a price moved since the cart showed it.
    /// </summary>
    public sealed class CartReview
    {
        public readonly IReadOnlyList<ReviewedCartLine> Buyable;
        public readonly IReadOnlyList<CartReviewIssue> Dropped;
        public readonly int LiveTotalCredits;
        public readonly int LiveTotalUsdCents;
        public readonly int UnitCount;

        /// <summary>Distinct transaction groups, i.e. the number of signatures the checkout will ask for.</summary>
        public readonly int GroupCount;
        public readonly bool OrderChanged;
        public readonly DateTime ReviewedAtUtc;

        public CartReview(IReadOnlyList<ReviewedCartLine> buyable, IReadOnlyList<CartReviewIssue> dropped, int liveTotalCredits, int liveTotalUsdCents,
            int unitCount, int groupCount, bool orderChanged, DateTime reviewedAtUtc)
        {
            Buyable = buyable;
            Dropped = dropped;
            LiveTotalCredits = liveTotalCredits;
            LiveTotalUsdCents = liveTotalUsdCents;
            UnitCount = unitCount;
            GroupCount = groupCount;
            OrderChanged = orderChanged;
            ReviewedAtUtc = reviewedAtUtc;
        }
    }

    public readonly struct CartReviewResult
    {
        public readonly CreditsPurchaseError Error;
        public readonly CartReview? Review;
        public readonly string? Message;

        public bool Success => Error == CreditsPurchaseError.None;

        public CartReviewResult(CreditsPurchaseError error, string? message = null)
        {
            Error = error;
            Review = null;
            Message = message;
        }

        private CartReviewResult(CartReview review)
        {
            Error = CreditsPurchaseError.None;
            Review = review;
            Message = null;
        }

        public static CartReviewResult Ok(CartReview review) =>
            new (review);
    }

    public enum CartCheckoutStage
    {
        Reviewing,
        Reserving,
        Signing,
        WaitingSettlement,
        Completed,
        Failed,
    }

    public readonly struct CartCheckoutProgress
    {
        public readonly CartCheckoutStage Stage;

        /// <summary>1-based index of the group being processed; 0 outside the per-group stages.</summary>
        public readonly int GroupIndex;
        public readonly int GroupCount;
        public readonly int UnitsReserved;
        public readonly int UnitCount;

        public CartCheckoutProgress(CartCheckoutStage stage, int groupIndex, int groupCount, int unitsReserved, int unitCount)
        {
            Stage = stage;
            GroupIndex = groupIndex;
            GroupCount = groupCount;
            UnitsReserved = unitsReserved;
            UnitCount = unitCount;
        }
    }

    public enum CartCheckoutOutcome
    {
        Completed,
        PartiallyCompleted,
        Failed,
        InsufficientCredits,
        Cancelled,
    }

    public sealed class CartGroupOutcome
    {
        public readonly string Key;
        public readonly CreditsListingKind Kind;
        public readonly int UnitCount;
        public readonly int UsdCents;
        public readonly string? CreditId;
        public readonly string? TxHash;
        public readonly bool Broadcast;
        public readonly bool Settled;
        public readonly bool Reverted;
        public readonly CreditsPurchaseError Error;
        public readonly string? Message;

        public CartGroupOutcome(string key, CreditsListingKind kind, int unitCount, int usdCents, string? creditId, string? txHash,
            bool broadcast, bool settled, bool reverted, CreditsPurchaseError error, string? message)
        {
            Key = key;
            Kind = kind;
            UnitCount = unitCount;
            UsdCents = usdCents;
            CreditId = creditId;
            TxHash = txHash;
            Broadcast = broadcast;
            Settled = settled;
            Reverted = reverted;
            Error = error;
            Message = message;
        }
    }

    /// <summary>
    ///     The terminal state of one checkout. Ownership follows SETTLEMENT, never broadcast: only the lines of a
    ///     mined group left the cart, a broadcast-then-reverted group bought nothing, and a still-pending group keeps
    ///     its credits held until the server reconciler settles it.
    /// </summary>
    public sealed class CartCheckoutResult
    {
        public readonly CartCheckoutOutcome Outcome;
        public readonly IReadOnlyList<CartGroupOutcome> Groups;
        public readonly IReadOnlyList<string> BoughtLineIds;
        public readonly IReadOnlyList<ReviewedCartLine> BoughtUnits;
        public readonly IReadOnlyList<ReviewedCartLine> UnboughtUnits;
        public readonly IReadOnlyList<string> SettledTxHashes;
        public readonly bool HasPendingSettlement;
        public readonly CreditsPurchaseError FirstError;
        public readonly string? Message;

        /// <summary>Credits the buyer is short of when the outcome is InsufficientCredits; -1 when the server did not say.</summary>
        public readonly int MissingCredits;
        public readonly CartCheckoutStage FailedAtStage;

        public bool AnyBought => BoughtLineIds.Count > 0;

        public CartCheckoutResult(CartCheckoutOutcome outcome, IReadOnlyList<CartGroupOutcome> groups, IReadOnlyList<string> boughtLineIds,
            IReadOnlyList<ReviewedCartLine> boughtUnits, IReadOnlyList<ReviewedCartLine> unboughtUnits, IReadOnlyList<string> settledTxHashes,
            bool hasPendingSettlement, CreditsPurchaseError firstError, string? message, int missingCredits, CartCheckoutStage failedAtStage)
        {
            Outcome = outcome;
            Groups = groups;
            BoughtLineIds = boughtLineIds;
            BoughtUnits = boughtUnits;
            UnboughtUnits = unboughtUnits;
            SettledTxHashes = settledTxHashes;
            HasPendingSettlement = hasPendingSettlement;
            FirstError = firstError;
            Message = message;
            MissingCredits = missingCredits;
            FailedAtStage = failedAtStage;
        }
    }
}
