namespace DCL.Web3
{
    /// <summary>
    ///     Indicates the source/origin of a Web3 request.
    /// </summary>
    public enum Web3RequestSource
    {
        /// <summary>
        ///     Request comes from an SDK scene (external).
        /// </summary>
        SDKScene,

        /// <summary>
        ///     Request comes from internal Explorer features (Gifting, Donations, etc.).
        /// </summary>
        Internal,
    }
}
