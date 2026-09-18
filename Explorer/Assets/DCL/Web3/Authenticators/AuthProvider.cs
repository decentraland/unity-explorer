namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Available authentication methods
    /// </summary>
    public enum AuthProvider
    {
        /// <summary>
        ///     ThirdWeb authentication, either as a guest or with an Email + OTP code
        /// </summary>
        ThirdWeb,

        /// <summary>
        ///     Dapp authentication via external browser wallet (MetaMask, etc.)
        /// </summary>
        Dapp,

        /// <summary>
        ///     Guest authentication with an account generated on the device, which lives no longer than its identity
        /// </summary>
        Ephemeral,
    }
}
