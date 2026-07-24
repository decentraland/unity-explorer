using System;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     How much the user can trust the recipient of a scene-initiated transfer, in decreasing order
    ///     of confidence. Drives the confirmation copy so a social-login user can judge the outcome
    ///     without reading a raw transaction.
    /// </summary>
    public enum RecipientGate
    {
        /// <summary>
        ///     No recipient gate applies (not a value transfer, or the user is sending to themselves).
        /// </summary>
        None,

        /// <summary>
        ///     The recipient has a Decentraland profile.
        /// </summary>
        Profile,

        /// <summary>
        ///     The recipient is the verified creator/donation wallet of the current scene.
        /// </summary>
        SceneCreator,

        /// <summary>
        ///     The recipient is an address with no Decentraland identity (outside Decentraland).
        /// </summary>
        External,
    }

    /// <summary>
    ///     Information about a transaction that requires user confirmation
    /// </summary>
    public class TransactionConfirmationRequest
    {
        private const string ETH_SEND_TRANSACTION = "eth_sendTransaction";

        public string Method { get; set; }
        public int ChainId { get; set; }

        public bool IsTransaction => string.Equals(Method, ETH_SEND_TRANSACTION, StringComparison.OrdinalIgnoreCase);
        public string? NetworkName { get; set; }
        public string? To { get; set; }
        public string? Value { get; set; }
        public string? Data { get; set; }
        public object[]? Params { get; set; }

        // Optional extra info (best-effort) for eth_sendTransaction UI
        public string? EstimatedGasFeeEth { get; set; }
        public string? BalanceEth { get; set; }

        /// <summary>
        ///     If true, hides the description text in the confirmation popup.
        ///     Used for internal features (like Gifting) that have their own UI with description.
        /// </summary>
        public bool HideDescription { get; set; }

        /// <summary>
        ///     If true, hides the transaction details panel (balance, gas fee) in the confirmation popup.
        ///     Used for internal features (like Gifting) that display this info in their own UI.
        /// </summary>
        public bool HideDetailsPanel { get; set; }

        /// <summary>
        ///     Recipient trust level for a scene-initiated transfer, resolved before the popup is shown.
        ///     <see cref="RecipientGate.None" /> when no gate applies.
        /// </summary>
        public RecipientGate Gate { get; set; } = RecipientGate.None;

        /// <summary>
        ///     The address that actually receives the assets (decoded from the calldata for token
        ///     transfers, so it is not necessarily the transaction's <see cref="To" />).
        /// </summary>
        public string? RecipientAddress { get; set; }

        /// <summary>
        ///     Human-readable recipient name to show inline: the profile name for
        ///     <see cref="RecipientGate.Profile" /> or the scene name for
        ///     <see cref="RecipientGate.SceneCreator" />. Null when there is no name to show.
        /// </summary>
        public string? RecipientName { get; set; }

        /// <summary>
        ///     The transfer amount formatted for display (e.g. "5"), or null when it cannot be determined
        ///     (e.g. an opaque contract call).
        /// </summary>
        public string? AmountDisplay { get; set; }

        /// <summary>
        ///     The asset symbol for the transfer (e.g. "MANA", "ETH"), or null when unknown.
        /// </summary>
        public string? AssetSymbol { get; set; }
    }
}
