using System;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Interface for handling Dapp wallet verification flow.
    ///     Used when user authenticates via browser wallet (MetaMask, WalletConnect, etc.)
    /// </summary>
    public interface IDappVerificationHandler
    {
        /// <summary>
        ///     Raised when verification code should be displayed to the user.
        ///     Always invoked on the main thread.
        /// </summary>
        public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired;
    }
}
