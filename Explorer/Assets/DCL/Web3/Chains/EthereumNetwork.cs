namespace DCL.Web3.Chains
{
    /// <summary>
    ///     The chain this run signs and transacts against, resolved once by <c>ChainUtils.ResolveNetwork</c> from the
    ///     environment and the <c>--eth-network</c> override.
    ///     <para>
    ///         Each value names an ethereum network only. Consumers that need the polygon side pair one with it by
    ///         convention - Polygon with mainnet, Amoy with sepolia - because a deployment is on both or neither: an
    ///         identity signed for mainnet has no business spending against test credits contracts, and a test
    ///         identity has no business against the real ones.
    ///     </para>
    /// </summary>
    public enum EthereumNetwork
    {
        Mainnet,
        Sepolia,
    }
}
