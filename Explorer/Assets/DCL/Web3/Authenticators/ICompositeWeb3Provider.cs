using Cysharp.Threading.Tasks;
using DCL.Web3;
using System;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Information about a transaction that requires user confirmation
    /// </summary>
    public class TransactionConfirmationRequest
    {
        public string Method { get; set; }
        public int ChainId { get; set; }
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
    }

    /// <summary>
    ///     Delegate for transaction confirmation callback.
    ///     Returns true if user confirms, false if user rejects.
    /// </summary>
    public delegate UniTask<bool> TransactionConfirmationDelegate(TransactionConfirmationRequest request);

    /// <summary>
    /// Interface for composite authentication provider that supports multiple authentication methods.
    /// Extends IWeb3VerifiedAuthenticator to provide authentication and adds method switching capability.
    /// Also implements IEthereumApi to ensure Web3 API calls use the correct provider.
    /// </summary>
    public interface ICompositeWeb3Provider : IWeb3VerifiedAuthenticator, IEthereumApi
    {
        /// <summary>
        /// Currently selected authentication method
        /// </summary>
        AuthProvider CurrentProvider { get; set; }

        /// <summary>
        /// Event fired when the authentication method changes
        /// </summary>
        event Action<AuthProvider>? OnMethodChanged;

        /// <summary>
        /// Returns true if ThirdWeb OTP method is currently selected
        /// </summary>
        bool IsThirdWebOTP { get; }

        /// <summary>
        /// Returns true if Dapp Wallet method is currently selected
        /// </summary>
        bool IsDappWallet { get; }

        /// <summary>
        ///     Sets the callback that will be invoked when a transaction requires user confirmation.
        ///     The callback should return true if user confirms, false if user rejects.
        /// </summary>
        void SetTransactionConfirmationCallback(TransactionConfirmationDelegate? callback);
    }
}
