using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Numerics;
using System.Text;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     NOTE: AI generated based on the shop repo trade-encoding
    ///     On-chain encoding for CreditsManager.useCredits(accept([trade])) and its gasless meta-transaction
    ///     wrapper. Port of the shop web app's single source of truth for these bytes
    ///     (Server/shop/app/src/lib/trade-encoding.ts and buy-gasless.ts) — every normalization here fixed a
    ///     real on-chain failure there, so keep them byte-identical (guarded by golden-vector tests).
    /// </summary>
    public static class CreditsTradeEncoder
    {
        public const int ASSET_TYPE_ERC20 = 1;
        public const int ASSET_TYPE_USD_PEGGED_MANA = 2;
        public const int ASSET_TYPE_ERC721 = 3;
        public const int ASSET_TYPE_COLLECTION_ITEM = 4;

        // The trades stored by marketplace-server keep checks.expiration/effective in milliseconds while the
        // contract validates seconds (block.timestamp); values above this threshold are milliseconds.
        private const long MILLISECONDS_THRESHOLD = 1_000_000_000_000;

        private const string ACCEPT_ABI = @"[{""name"":""accept"",""type"":""function"",""outputs"":[],""inputs"":[{""name"":""_trades"",""type"":""tuple[]"",""components"":[
            {""name"":""signer"",""type"":""address""},
            {""name"":""signature"",""type"":""bytes""},
            {""name"":""checks"",""type"":""tuple"",""components"":[
                {""name"":""uses"",""type"":""uint256""},
                {""name"":""expiration"",""type"":""uint256""},
                {""name"":""effective"",""type"":""uint256""},
                {""name"":""salt"",""type"":""bytes32""},
                {""name"":""contractSignatureIndex"",""type"":""uint256""},
                {""name"":""signerSignatureIndex"",""type"":""uint256""},
                {""name"":""allowedRoot"",""type"":""bytes32""},
                {""name"":""allowedProof"",""type"":""bytes32[]""},
                {""name"":""externalChecks"",""type"":""tuple[]"",""components"":[
                    {""name"":""contractAddress"",""type"":""address""},
                    {""name"":""selector"",""type"":""bytes4""},
                    {""name"":""value"",""type"":""bytes""},
                    {""name"":""required"",""type"":""bool""}]}]},
            {""name"":""sent"",""type"":""tuple[]"",""components"":[
                {""name"":""assetType"",""type"":""uint256""},
                {""name"":""contractAddress"",""type"":""address""},
                {""name"":""value"",""type"":""uint256""},
                {""name"":""beneficiary"",""type"":""address""},
                {""name"":""extra"",""type"":""bytes""}]},
            {""name"":""received"",""type"":""tuple[]"",""components"":[
                {""name"":""assetType"",""type"":""uint256""},
                {""name"":""contractAddress"",""type"":""address""},
                {""name"":""value"",""type"":""uint256""},
                {""name"":""beneficiary"",""type"":""address""},
                {""name"":""extra"",""type"":""bytes""}]}]}]}]";

        private const string USE_CREDITS_ABI = @"[{""name"":""useCredits"",""type"":""function"",""outputs"":[],""inputs"":[{""name"":""_args"",""type"":""tuple"",""components"":[
            {""name"":""credits"",""type"":""tuple[]"",""components"":[
                {""name"":""value"",""type"":""uint256""},
                {""name"":""expiresAt"",""type"":""uint256""},
                {""name"":""salt"",""type"":""bytes32""}]},
            {""name"":""creditsSignatures"",""type"":""bytes[]""},
            {""name"":""externalCall"",""type"":""tuple"",""components"":[
                {""name"":""target"",""type"":""address""},
                {""name"":""selector"",""type"":""bytes4""},
                {""name"":""data"",""type"":""bytes""},
                {""name"":""expiresAt"",""type"":""uint256""},
                {""name"":""salt"",""type"":""bytes32""}]},
            {""name"":""customExternalCallSignature"",""type"":""bytes""},
            {""name"":""maxUncreditedValue"",""type"":""uint256""},
            {""name"":""maxCreditedValue"",""type"":""uint256""}]}]}]";

        private const string EXECUTE_META_TX_ABI = @"[{""name"":""executeMetaTransaction"",""type"":""function"",""outputs"":[],""inputs"":[
            {""name"":""_userAddress"",""type"":""address""},
            {""name"":""_functionData"",""type"":""bytes""},
            {""name"":""_signature"",""type"":""bytes""}]}]";

        private const string GET_NONCE_ABI = @"[{""name"":""getNonce"",""type"":""function"",""outputs"":[],""inputs"":[
            {""name"":""_signer"",""type"":""address""}]}]";

        private static readonly FunctionABI ACCEPT_FUNCTION = DeserialiseFunction(ACCEPT_ABI);
        private static readonly FunctionABI USE_CREDITS_FUNCTION = DeserialiseFunction(USE_CREDITS_ABI);
        private static readonly FunctionABI EXECUTE_META_TX_FUNCTION = DeserialiseFunction(EXECUTE_META_TX_ABI);
        private static readonly FunctionABI GET_NONCE_FUNCTION = DeserialiseFunction(GET_NONCE_ABI);

        private static readonly FunctionCallEncoder FUNCTION_CALL_ENCODER = new ();
        private static readonly ParametersEncoder PARAMETERS_ENCODER = new ();

        /// <summary>
        ///     accept([trade]) split into the (selector, data) pair consumed by the useCredits externalCall
        ///     struct: the selector is the 4-byte sighash, the data is the ABI-encoded parameters only.
        /// </summary>
        public static (byte[] selector, byte[] data) BuildAcceptCall(TradeDto trade, string buyer)
        {
            object[] tradeTree = BuildOnChainTradeTree(trade, buyer);
            byte[] data = PARAMETERS_ENCODER.EncodeParameters(ACCEPT_FUNCTION.InputParameters, new object[] { new object[] { tradeTree } });
            byte[] selector = ACCEPT_FUNCTION.Sha3Signature.HexToByteArray();
            return (selector, data);
        }

        /// <summary>
        ///     Full useCredits(UseCreditsArgs) calldata spending one authorized credit on one trade.
        ///     externalCallExpiresAt (unix seconds) and externalCallSalt (32 bytes) are injected by the caller
        ///     so the encoding stays deterministic for tests.
        /// </summary>
        public static string BuildUseCreditsCalldata(
            TradeDto trade,
            string buyer,
            AuthorizedCredit credit,
            string maxCreditedValue,
            long externalCallExpiresAt,
            byte[] externalCallSalt)
        {
            (byte[] acceptSelector, byte[] acceptData) = BuildAcceptCall(trade, buyer);

            BigInteger maxCredited = BigInteger.Parse(maxCreditedValue);
            BigInteger uncredited = UncreditedValue(maxCreditedValue, credit.availableAmount);

            var creditsTree = new object[]
            {
                new object[] { BigInteger.Parse(credit.amount), new BigInteger(credit.expiresAt), IdToSalt(credit.id) },
            };

            var argsTree = new object[]
            {
                creditsTree,
                new[] { credit.signature.HexToByteArray() },
                new object[] { trade.contract, acceptSelector, acceptData, new BigInteger(externalCallExpiresAt), externalCallSalt },
                Array.Empty<byte>(),
                uncredited,
                maxCredited,
            };

            return FUNCTION_CALL_ENCODER.EncodeRequest(USE_CREDITS_FUNCTION.Sha3Signature, USE_CREDITS_FUNCTION.InputParameters, new object[] { argsTree });
        }

        /// <summary>
        ///     The MANA the buyer covers from their own balance: whatever the server's cap exceeds the credit's
        ///     available amount by. Zero for the ephemeral credits this flow authorizes, which the credits-server
        ///     sizes exactly to their trade.
        /// </summary>
        public static BigInteger UncreditedValue(string maxCreditedValue, string availableAmount)
        {
            BigInteger uncredited = BigInteger.Parse(maxCreditedValue) - BigInteger.Parse(availableAmount);
            return uncredited < BigInteger.Zero ? BigInteger.Zero : uncredited;
        }

        /// <summary>
        ///     executeMetaTransaction(userAddress, functionData, signature) calldata — the 3-arg CreditsManager
        ///     variant that takes the whole 65-byte signature (not the legacy r/s/v split).
        /// </summary>
        public static string BuildExecuteMetaTxCalldata(string from, string functionData, string signature) =>
            FUNCTION_CALL_ENCODER.EncodeRequest(
                EXECUTE_META_TX_FUNCTION.Sha3Signature,
                EXECUTE_META_TX_FUNCTION.InputParameters,
                from, functionData.HexToByteArray(), signature.HexToByteArray());

        /// <summary>
        ///     getNonce(signer) calldata for the CreditsManager meta-transaction replay protection read.
        /// </summary>
        public static string BuildGetNonceCalldata(string signer) =>
            FUNCTION_CALL_ENCODER.EncodeRequest(GET_NONCE_FUNCTION.Sha3Signature, GET_NONCE_FUNCTION.InputParameters, signer);

        /// <summary>
        ///     The prefixed 4-byte sighash of a single-function ABI — the entire calldata of a parameterless call.
        /// </summary>
        public static string SighashOf(string abiJson) =>
            $"0x{DeserialiseFunction(abiJson).Sha3Signature}";

        /// <summary>
        ///     eth_signTypedData_v4 payload for the Decentraland/Polygon native meta-transaction: domain
        ///     salt = bytes32(chainId), primary type MetaTransaction { nonce, from, functionData }.
        /// </summary>
        public static string BuildMetaTxTypedDataJson(CreditsChainConfig chainConfig, BigInteger nonce, string from, string functionData)
        {
            var typedData = new JObject
            {
                ["types"] = new JObject
                {
                    ["EIP712Domain"] = new JArray
                    {
                        TypeEntry("name", "string"),
                        TypeEntry("version", "string"),
                        TypeEntry("verifyingContract", "address"),
                        TypeEntry("salt", "bytes32"),
                    },
                    ["MetaTransaction"] = new JArray
                    {
                        TypeEntry("nonce", "uint256"),
                        TypeEntry("from", "address"),
                        TypeEntry("functionData", "bytes"),
                    },
                },
                ["domain"] = new JObject
                {
                    ["name"] = chainConfig.CreditsManagerEip712Name,
                    ["version"] = chainConfig.CreditsManagerEip712Version,
                    ["verifyingContract"] = chainConfig.CreditsManagerAddress,
                    ["salt"] = ChainIdSalt(chainConfig.ChainId),
                },
                ["primaryType"] = "MetaTransaction",
                ["message"] = new JObject
                {
                    ["nonce"] = nonce.ToString(),
                    ["from"] = from,
                    ["functionData"] = functionData,
                },
            };

            return typedData.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        ///     USD-pegged amount (USD wei, 1e18 = $1) to cents, rounded up so the authorized credit never
        ///     under-covers what the trade settles for (a short credit reverts useCredits on-chain).
        /// </summary>
        public static int UsdWeiToCents(string? amount)
        {
            if (string.IsNullOrEmpty(amount))
                return 0;

            BigInteger wei = BigInteger.Parse(amount);
            return CeilToCents(wei);
        }

        /// <summary>
        ///     A trade asset's raw wei amount, zero when the asset carries none.
        /// </summary>
        public static BigInteger AmountOrZero(string? amount) =>
            string.IsNullOrEmpty(amount) ? BigInteger.Zero : BigInteger.Parse(amount);

        /// <summary>
        ///     MANA wei to USD cents at the given oracle rate, rounded up. Port of the shop web app's
        ///     mana-rate.ts manaWeiToUsdCents: a legacy listing is priced in MANA, so its USD price only exists
        ///     through the rate — and it has to be the rate settlement uses, never a catalogue snapshot.
        /// </summary>
        public static int ManaWeiToUsdCents(string? manaWei, in ManaUsdRate rate)
        {
            if (string.IsNullOrEmpty(manaWei))
                return 0;

            return CeilToCents(BigInteger.Parse(manaWei) * rate.Rate / BigInteger.Pow(10, rate.Decimals));
        }

        /// <summary>
        ///     USD-pegged amount (USD wei) to the MANA wei the marketplace transfers for it — the conversion
        ///     the on-chain accept performs for USD_PEGGED_MANA assets, so the CreditsManager's MANA cap can be
        ///     checked against what the trade will actually draw.
        /// </summary>
        public static BigInteger UsdWeiToManaWei(string? usdWei, in ManaUsdRate rate)
        {
            if (string.IsNullOrEmpty(usdWei))
                return BigInteger.Zero;

            return BigInteger.Parse(usdWei) * BigInteger.Pow(10, rate.Decimals) / rate.Rate;
        }

        /// <summary>
        ///     Cents rounded up to a whole credit. The credits-server charges whole credits
        ///     (authorize-usd-credit.ts rounds the request up itself), so quoting the rounded amount is what
        ///     keeps the price the buyer confirms equal to the price that gets locked.
        /// </summary>
        public static int RoundUpToWholeCredit(int cents, int centsPerCredit) =>
            (cents + centsPerCredit - 1) / centsPerCredit * centsPerCredit;

        /// <summary>
        ///     bytes32(chainId) — the DCL meta-tx domain salt.
        /// </summary>
        public static string ChainIdSalt(int chainId)
        {
            var bytes = new byte[32];
            bytes[28] = (byte)((chainId >> 24) & 0xFF);
            bytes[29] = (byte)((chainId >> 16) & 0xFF);
            bytes[30] = (byte)((chainId >> 8) & 0xFF);
            bytes[31] = (byte)(chainId & 0xFF);
            return bytes.ToHex(true);
        }

        /// <summary>
        ///     Credit ids arrive either as hex (padded to bytes32) or as plain strings (UTF-8 bytes padded to
        ///     bytes32) — they double as the on-chain credit salt.
        /// </summary>
        public static byte[] IdToSalt(string? id)
        {
            if (id == null || id.Length == 0)
                return new byte[32];

            byte[] raw = id.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? id.HexToByteArray()
                : Encoding.UTF8.GetBytes(id);

            return LeftPad32(raw);
        }

        private static int CeilToCents(BigInteger usdWei)
        {
            BigInteger centWei = BigInteger.Pow(10, 16);
            return (int)((usdWei + centWei - BigInteger.One) / centWei);
        }

        private static object[] BuildOnChainTradeTree(TradeDto trade, string buyer)
        {
            TradeChecksDto checks = trade.checks;
            ExternalCheckDto[] externalChecks = checks.externalChecks ?? Array.Empty<ExternalCheckDto>();
            var externalChecksTree = new object[externalChecks.Length];

            for (var i = 0; i < externalChecks.Length; i++)
            {
                ExternalCheckDto check = externalChecks[i];
                externalChecksTree[i] = new object[] { check.contractAddress, check.selector.HexToByteArray(), HexOrEmpty(check.value), check.required };
            }

            var checksTree = new object[]
            {
                new BigInteger(checks.uses),
                new BigInteger(ToChainSeconds(checks.expiration)),
                new BigInteger(ToChainSeconds(checks.effective)),
                LeftPad32(checks.salt.HexToByteArray()),
                new BigInteger(checks.contractSignatureIndex),
                new BigInteger(checks.signerSignatureIndex),
                NormalizeAllowedRoot(checks.allowedRoot),
                Array.Empty<byte[]>(),
                externalChecksTree,
            };

            return new object[]
            {
                trade.signer,
                trade.signature.HexToByteArray(),
                checksTree,
                BuildAssetsTree(trade.sent, buyer, useAssetBeneficiary: false),
                BuildAssetsTree(trade.received, buyer, useAssetBeneficiary: true),
            };
        }

        private static object[] BuildAssetsTree(TradeAssetDto[] assets, string buyer, bool useAssetBeneficiary)
        {
            var tree = new object[assets.Length];

            for (var i = 0; i < assets.Length; i++)
            {
                TradeAssetDto asset = assets[i];

                string beneficiary = useAssetBeneficiary && !string.IsNullOrEmpty(asset.beneficiary)
                    ? asset.beneficiary!
                    : buyer;

                tree[i] = new object[]
                {
                    new BigInteger(asset.assetType),
                    asset.contractAddress,
                    ValueForAsset(asset),
                    beneficiary,
                    HexOrEmpty(asset.extra),
                };
            }

            return tree;
        }

        private static BigInteger ValueForAsset(TradeAssetDto asset) =>
            asset.assetType switch
            {
                ASSET_TYPE_ERC721 => BigInteger.Parse(asset.tokenId!),
                ASSET_TYPE_COLLECTION_ITEM => BigInteger.Parse(asset.itemId!),
                ASSET_TYPE_ERC20 or ASSET_TYPE_USD_PEGGED_MANA => BigInteger.Parse(asset.amount!),
                _ => throw new NotSupportedException($"Unsupported assetType {asset.assetType}"),
            };

        private static long ToChainSeconds(long value) =>
            value > MILLISECONDS_THRESHOLD ? value / 1000 : value;

        private static byte[] NormalizeAllowedRoot(string? allowedRoot)
        {
            if (string.IsNullOrEmpty(allowedRoot) || allowedRoot == "0x")
                return new byte[32];

            return LeftPad32(allowedRoot.HexToByteArray());
        }

        private static byte[] HexOrEmpty(string? hex) =>
            string.IsNullOrEmpty(hex) ? Array.Empty<byte>() : hex.HexToByteArray();

        private static byte[] LeftPad32(byte[] value)
        {
            if (value.Length == 32)
                return value;

            if (value.Length > 32)
                throw new ArgumentException($"Value is {value.Length} bytes, cannot pad to bytes32");

            var padded = new byte[32];
            Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }

        private static JObject TypeEntry(string name, string type) =>
            new () { ["name"] = name, ["type"] = type };

        private static FunctionABI DeserialiseFunction(string abiJson)
        {
            ContractABI contractAbi = ABIDeserialiserFactory.DeserialiseContractABI(abiJson);
            return contractAbi.Functions[0];
        }
    }
}
