namespace DCL.Web3
{
    /// <summary>
    ///     Indicates the source/origin of a Web3 request.
    ///     Used to determine whether to show confirmation UI.
    /// </summary>
    public enum Web3RequestSource
    {
        /// <summary>
        ///     Request comes from an SDK scene (external).
        ///     Requires user confirmation UI for ThirdWeb provider.
        /// </summary>
        SDKScene,

        /// <summary>
        ///     Request comes from internal Explorer features (Gifting, Donations, etc.).
        ///     Skips confirmation UI for ThirdWeb provider since it's already handled by feature-specific UI.
        /// </summary>
        Internal,
    }
}
