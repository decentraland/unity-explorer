#nullable enable
using System;

// ReSharper disable InconsistentNaming
namespace DCL.MarketplaceCredits
{
    // Server schema: credits-server POST /credits/authorize/batch (Server/shop/app/src/lib/credits.ts AuthorizeGroupResult).
    // ONE credit for the whole group: maxCreditedValue equals the credit's amount because a group's cap IS its cap.
    [Serializable]
    public struct AuthorizeGroupResponse
    {
        public AuthorizedCredit credit;
        public string maxCreditedValue;
        public long usdCents;
        public string oracleRate;
        public AuthorizedGroupLine[] lines;
    }

    // Server schema: credits-server POST /credits/authorize/batch response `lines[]`.
    [Serializable]
    public struct AuthorizedGroupLine
    {
        public long usdCents;
        public string? tradeId;
        public string? contractAddress;
        public string? itemId;
    }

    // Server schema: credits-server POST /credits/authorize/batch request body (Newtonsoft; null keys are omitted).
    [Serializable]
    public class AuthorizeUsdCreditGroupBody
    {
        public AuthorizeUsdCreditGroupLineBody[] items = null!;
        public string source = null!;
    }

    [Serializable]
    public class AuthorizeUsdCreditGroupLineBody
    {
        public int usdPriceCents;
        public string? tradeId;
        public string? contractAddress;
        public string? itemId;
    }

    // Server schema: credits-server POST /credits/authorize/submitted request body.
    [Serializable]
    public struct ReportIntentSubmissionBody
    {
        public string[] salts;
        public string txHash;
    }

    /// <summary>
    ///     One unit of a checkout as the credits-server authorizes it: its price plus what is being bought. A
    ///     trade sends its id; every unit with a known item also sends the pair so the purchase history can name it.
    /// </summary>
    public readonly struct CheckoutLine
    {
        public readonly int UsdPriceCents;
        public readonly string? TradeId;
        public readonly string? ContractAddress;
        public readonly string? ItemId;

        public CheckoutLine(int usdPriceCents, string? tradeId, string? contractAddress, string? itemId)
        {
            UsdPriceCents = usdPriceCents;
            TradeId = tradeId;
            ContractAddress = contractAddress;
            ItemId = itemId;
        }
    }

    public enum CreditsAuthorizeError
    {
        Cancelled,
        InsufficientCredits,
        TooManyLiveIntents,
        FeatureDisabled,
        BadRequest,
        NetworkError,
    }
}
