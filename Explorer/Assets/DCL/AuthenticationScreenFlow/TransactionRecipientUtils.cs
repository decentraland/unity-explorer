using DCL.Web3;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DCL.AuthenticationScreenFlow
{
    /// <summary>
    ///     Builds the plain-language copy of the recipient confirmation step: how much is being sent and
    ///     to whom.
    /// </summary>
    public static class TransactionRecipientUtils
    {
        /// <summary>
        ///     Copy for a request that matches no mapped shape, so neither the amount nor the recipient is
        ///     known and the raw payload of the first step is all there is to go on.
        /// </summary>
        public const string UNKNOWN_REQUEST_DESCRIPTION =
            "Are you sure you want to approve this request? Decentraland cannot tell what it does or who receives your assets.";

        // Link/highlight blue used for names and addresses in the confirmation copy.
        private const string HIGHLIGHT_COLOR = "#32CEFF";

        // MANA renders as a sprite, which must exist in the description's TMP sprite asset.
        private const string MANA_SPRITE = "<sprite name=\"MANA\">";

        // The popup labels gas and balance in "ETH"; keep the native transfer symbol consistent.
        private const string NATIVE_SYMBOL = "ETH";

        // Both the native currency and MANA use 18 decimals.
        private const int TOKEN_DECIMALS = 18;
        private const int MAX_FRACTION_DIGITS = 4;

        // Known Decentraland MANA contracts (https://contracts.decentraland.org/addresses.json, same
        // source as DonationsService). Any network matches: the symbol is display-only and a scene may
        // transfer MANA on either network.
        private static readonly HashSet<string> MANA_CONTRACTS = new (StringComparer.OrdinalIgnoreCase)
        {
            "0x0f5d2fb29fb7d3cfee444a200298f468908cc942", // Ethereum Mainnet
            "0xe7fdae84acaba2a5ba817b6e6d8a2d415dbfedbe", // Ethereum Goerli
            "0xfa04d2e2ba9aec166c93dfeeba7427b2303befa9", // Ethereum Sepolia
            "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4", // Polygon Mainnet (PoS)
            "0x882da5967c435ea5cc6b09150d55e8304b838f45", // Polygon Mumbai Testnet
            "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0", // Polygon Amoy Testnet
        };

        public static string ProfileDescription(string amount, string address, string name) =>
            $"Are you sure you want to send {amount} to {HighlightLink(address, "@" + name)}?";

        public static string SceneCreatorDescription(string amount, string? sceneName) =>
            $"Are you sure you want to send {amount} to the creator of {Highlight(string.IsNullOrEmpty(sceneName) ? "this scene" : sceneName!)}?";

        public static string ExternalWalletDescription(string amount, string address) =>
            $"Are you sure you want to send {amount} to a wallet outside of Decentraland: {Highlight(address)}?";

        /// <summary>
        ///     "5 &lt;MANA sprite&gt;" for MANA, "5 ETH" for the native currency, or "assets" when the
        ///     amount cannot be determined (an unknown token or an opaque contract call).
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
            !string.IsNullOrEmpty(tokenContract) && MANA_CONTRACTS.Contains(tokenContract!);

        private static string FormatUnits(BigInteger amount)
        {
            BigInteger divisor = BigInteger.Pow(10, TOKEN_DECIMALS);
            BigInteger whole = amount / divisor;
            string fraction = (amount % divisor).ToString().PadLeft(TOKEN_DECIMALS, '0')[..MAX_FRACTION_DIGITS].TrimEnd('0');

            return fraction.Length == 0 ? whole.ToString() : $"{whole}.{fraction}";
        }

        private static string Highlight(string value) =>
            $"<color={HIGHLIGHT_COLOR}>{EscapeRichText(value)}</color>";

        private static string HighlightLink(string id, string label) =>
            $"<link=\"{EscapeRichText(id)}\"><color={HIGHLIGHT_COLOR}><b>{EscapeRichText(label)}</b></color></link>";

        /// <summary>
        ///     Swaps the characters TMP reads as markup for lookalikes that it does not. The copy is
        ///     rendered as rich text by design (the MANA sprite, the profile link), so a display name or
        ///     scene name carrying "&lt;size=0&gt;" would otherwise hide the warning it appears inside.
        /// </summary>
        private static string EscapeRichText(string value) =>
            value.Replace('<', '‹') // single left-pointing angle quotation mark
                 .Replace('>', '›') // single right-pointing angle quotation mark
                 .Replace('"', '”'); // right double quotation mark, closes the link attribute
    }
}
