namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Available authentication methods
    /// </summary>
    public enum AuthMethod
    {
        /// <summary>
        ///     ThirdWeb authentication via Email + OTP code
        /// </summary>
        ThirdWebOTP,

        /// <summary>
        ///     Dapp authentication via external browser wallet (MetaMask, etc.)
        /// </summary>
        DappWallet,
    }
}
