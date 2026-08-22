using DCL.UI;
using DCL.Utility;
using DCL.Web3;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DCL.AuthenticationScreenFlow
{
    /// <summary>
    ///     Builds the plain-language copy of the recipient confirmation step.
    /// </summary>
    public static class TransactionRecipientUtils
    {
        public const string UNKNOWN_REQUEST_DESCRIPTION =
            "Are you sure you want to approve this request? Decentraland cannot tell what it does or who receives your assets.";

        private const string HIGHLIGHT_COLOR = "#32CEFF";

        // TMP matches sprite names case-sensitively against the PolygonManaIcon character table, which
        // spells it "Mana"; anything else is tofu.
        private const string MANA_SPRITE = "<sprite name=\"Mana\">";

        // The popup labels gas and balance in "ETH"; keep the native transfer symbol consistent.
        private const string NATIVE_SYMBOL = "ETH";

        // Both the native currency and MANA use 18 decimals.
        private const int TOKEN_DECIMALS = 18;
        private const int MAX_FRACTION_DIGITS = 4;

        // https://contracts.decentraland.org/addresses.json, same source as DonationsService. Any network
        // matches: the symbol is display-only and a scene may transfer MANA on either.
        private static readonly ReadOnlyHashSet<string> MANA_CONTRACTS = new (new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "0x0f5d2fb29fb7d3cfee444a200298f468908cc942", // Ethereum Mainnet
            "0xe7fdae84acaba2a5ba817b6e6d8a2d415dbfedbe", // Ethereum Goerli
            "0xfa04d2e2ba9aec166c93dfeeba7427b2303befa9", // Ethereum Sepolia
            "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4", // Polygon Mainnet (PoS)
            "0x882da5967c435ea5cc6b09150d55e8304b838f45", // Polygon Mumbai Testnet
            "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0", // Polygon Amoy Testnet
        });

        public static string ProfileDescription(string amount, string address, string name) =>
            $"Are you sure you want to send {amount} to {HighlightLink(address, "@" + name)}?";

        public static string SceneCreatorDescription(string amount, string? sceneName) =>
            $"Are you sure you want to send {amount} to the creator of {Highlight(string.IsNullOrEmpty(sceneName) ? "this scene" : sceneName)}?";

        public static string ExternalWalletDescription(string amount, string address) =>
            $"Are you sure you want to send {amount} to a wallet outside of Decentraland: {Highlight(address)}?";

        /// <summary>
        ///     "5 &lt;MANA sprite&gt;", "5 ETH", or "assets" when the amount cannot be determined.
        /// </summary>
        public static string Amount(DecodedTransaction decoded)
        {
            switch (decoded.Kind)
            {
                case TransactionKind.NativeTransfer:
                    return $"{FormatUnits(decoded.Amount)} {NATIVE_SYMBOL}";
                case TransactionKind.Erc20Transfer when IsMana(decoded.TokenContract):
                    return $"{FormatUnits(decoded.Amount)} {MANA_SPRITE}";
                default:
                    return "assets";
            }
        }

        private static bool IsMana(string? tokenContract) =>
            !string.IsNullOrEmpty(tokenContract) && MANA_CONTRACTS.Contains(tokenContract);

        private static string FormatUnits(BigInteger amount)
        {
            if (amount.IsZero)
                return "0";

            BigInteger divisor = BigInteger.Pow(10, TOKEN_DECIMALS);
            BigInteger whole = amount / divisor;
            string fraction = (amount % divisor).ToString().PadLeft(TOKEN_DECIMALS, '0')[..MAX_FRACTION_DIGITS].TrimEnd('0');

            // "0" on a confirmation reads as sending nothing at all. Worded rather than written "<0.0001"
            // because the amount is interpolated into rich text unescaped, so it must not introduce a '<'.
            if (whole.IsZero && fraction.Length == 0)
                return "under 0.0001";

            return fraction.Length == 0 ? whole.ToString() : $"{whole}.{fraction}";
        }

        // The copy is rich text by design, so a display name carrying "<size=0>" would hide the warning it
        // sits in. The link id is escaped as an attribute because a '"' there would close it early.
        private static string Highlight(string value) =>
            $"<color={HIGHLIGHT_COLOR}>{RichTextSanitizer.Escape(value)}</color>";

        private static string HighlightLink(string id, string label) =>
            $"<link=\"{RichTextSanitizer.EscapeAttribute(id)}\"><color={HIGHLIGHT_COLOR}><b>{RichTextSanitizer.Escape(label)}</b></color></link>";
    }
}
