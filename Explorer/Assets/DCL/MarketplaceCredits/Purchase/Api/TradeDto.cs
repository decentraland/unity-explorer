using System;

// ReSharper disable InconsistentNaming
namespace DCL.MarketplaceCredits.Purchase
{
    // Server schema: marketplace-server GET /v1/trades/:id (@dcl/schemas Trade, unwrapped from { ok, data }).
    [Serializable]
    public class TradeDto
    {
        public string id = null!;
        public string signer = null!;
        public string signature = null!;
        public string type = null!;
        public string network = null!;
        public int chainId;

        // OffChainMarketplace contract that verifies the trade signature; server-authoritative.
        public string contract = null!;
        public TradeChecksDto checks = null!;
        public TradeAssetDto[] sent = null!;
        public TradeAssetDto[] received = null!;
    }

    // Server schema: @dcl/schemas TradeChecks. expiration/effective are stored in MILLISECONDS while the
    // chain validates SECONDS — normalized by CreditsTradeEncoder.
    [Serializable]
    public class TradeChecksDto
    {
        public long uses;
        public long expiration;
        public long effective;
        public string salt = null!;
        public long contractSignatureIndex;
        public long signerSignatureIndex;
        public string? allowedRoot;
        public ExternalCheckDto[]? externalChecks;
    }

    // Server schema: @dcl/schemas TradeExternalCheck.
    [Serializable]
    public class ExternalCheckDto
    {
        public string contractAddress = null!;
        public string selector = null!;
        public string? value;
        public bool required;
    }

    // Server schema: @dcl/schemas TradeAsset. assetType: 1 ERC20, 2 USD_PEGGED_MANA, 3 ERC721, 4 COLLECTION_ITEM.
    // The value field differs per assetType: amount (1/2), tokenId (3), itemId (4).
    [Serializable]
    public class TradeAssetDto
    {
        public int assetType;
        public string contractAddress = null!;
        public string? amount;
        public string? tokenId;
        public string? itemId;
        public string? extra;
        public string? beneficiary;
    }

    // Server schema: marketplace-server GET /v1/trades/:id response envelope.
    [Serializable]
    public class TradeResponse
    {
        public bool ok;
        public TradeDto? data;
    }
}
