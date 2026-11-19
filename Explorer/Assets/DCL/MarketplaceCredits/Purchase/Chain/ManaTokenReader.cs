using Cysharp.Threading.Tasks;
using DCL.Web3;
using System.Numerics;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     Reads the buyer's Polygon MANA: the balance a purchase can draw from and the allowance a contract
    ///     already holds over it. Both throw when the node gives no answer.
    /// </summary>
    public class ManaTokenReader
    {
        private readonly IEthereumApi ethereumApi;
        private readonly CreditsChainConfig chainConfig;

        public ManaTokenReader(IEthereumApi ethereumApi, CreditsChainConfig chainConfig)
        {
            this.ethereumApi = ethereumApi;
            this.chainConfig = chainConfig;
        }

        public virtual UniTask<BigInteger> BalanceOfAsync(string owner, CancellationToken ct) =>
            CallAsync(CreditsTradeEncoder.BuildBalanceOfCalldata(owner), ct);

        public virtual UniTask<BigInteger> AllowanceAsync(string owner, string spender, CancellationToken ct) =>
            CallAsync(CreditsTradeEncoder.BuildAllowanceCalldata(owner, spender), ct);

        private UniTask<BigInteger> CallAsync(string calldata, CancellationToken ct) =>
            EthCallReader.ReadUint256Async(ethereumApi, chainConfig.ReadonlyNetwork, chainConfig.ManaTokenContract.Address, calldata, ct);
    }
}
