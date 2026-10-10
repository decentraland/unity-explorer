using System;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Information about a transaction that requires user confirmation
    /// </summary>
    public class TransactionConfirmationRequest
    {
        private const string ETH_SEND_TRANSACTION = "eth_sendTransaction";
        private const string ETH_SIGN_TYPED_DATA_V4 = "eth_signTypedData_v4";

        public string Method { get; set; }
        public int ChainId { get; set; }

        public bool IsTransaction => string.Equals(Method, ETH_SEND_TRANSACTION, StringComparison.OrdinalIgnoreCase);

        public bool IsTypedDataSignature => string.Equals(Method, ETH_SIGN_TYPED_DATA_V4, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     The two methods that can move assets. Every other one only proves account ownership.
        /// </summary>
        public bool MovesAssets => IsTransaction || IsTypedDataSignature;

        public string? NetworkName { get; set; }
        public string? To { get; set; }
        public string? Value { get; set; }
        public string? Data { get; set; }
        public object[]? Params { get; set; }

        /// <summary>
        ///     The EIP-712 payload of an <see cref="IsTypedDataSignature" /> request.
        /// </summary>
        public string? TypedData { get; set; }

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
    }
}
