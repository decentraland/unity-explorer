using DCL.Web3;
using System;

namespace DCL.Web3.Authenticators
{
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
        AuthMethod CurrentMethod { get; set; }

        /// <summary>
        /// Event fired when the authentication method changes
        /// </summary>
        event Action<AuthMethod>? OnMethodChanged;

        /// <summary>
        /// Returns true if ThirdWeb OTP method is currently selected
        /// </summary>
        bool IsThirdWebOTP { get; }

        /// <summary>
        /// Returns true if Dapp Wallet method is currently selected
        /// </summary>
        bool IsDappWallet { get; }
    }
}
