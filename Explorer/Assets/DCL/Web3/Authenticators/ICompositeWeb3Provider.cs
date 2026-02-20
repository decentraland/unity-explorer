using Cysharp.Threading.Tasks;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Delegate for transaction confirmation callback.
    ///     Returns true if user confirms, false if user rejects.
    /// </summary>
    public delegate UniTask<bool> TransactionConfirmationDelegate(TransactionConfirmationRequest request);

    /// <summary>
    ///     Interface for composite authentication provider that supports multiple authentication methods.
    ///     Combines base authentication, Ethereum API, Dapp verification, and OTP flows.
    ///     This is the single entry point for all Web3 authentication needs.
    /// </summary>
    public interface ICompositeWeb3Provider : IWeb3Authenticator, IEthereumApi, IDappVerificationHandler, IOtpAuthenticator
    {
        /// <summary>
        /// Currently selected authentication method
        /// </summary>
        AuthProvider CurrentProvider { get; set; }

        /// <summary>
        /// Returns true if ThirdWeb OTP method is currently selected
        /// </summary>
        bool IsThirdWebOTP { get; }

        /// <summary>
        ///     Sets the callback that will be invoked when a transaction requires user confirmation.
        ///     The callback should return true if user confirms, false if user rejects.
        /// </summary>
        void SetTransactionConfirmationCallback(TransactionConfirmationDelegate? callback);
    }
}
