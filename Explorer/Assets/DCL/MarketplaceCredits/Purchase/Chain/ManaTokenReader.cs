using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3;
using System;
using System.Numerics;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     Reads the buyer's Polygon MANA: the balance a purchase can draw from and the allowance a contract
    ///     already holds over it.
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

        /// <summary>The balance, or null when it could not be read.</summary>
        public async UniTask<BigInteger?> TryBalanceOfAsync(string owner, CancellationToken ct)
        {
            try { return await BalanceOfAsync(owner, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA balance unavailable: {e.Message}");
                return null;
            }
        }

        public virtual UniTask<BigInteger> AllowanceAsync(string owner, string spender, CancellationToken ct) =>
            CallAsync(CreditsTradeEncoder.BuildAllowanceCalldata(owner, spender), ct);

        private UniTask<BigInteger> CallAsync(string calldata, CancellationToken ct) =>
            EthCallReader.ReadUint256Async(ethereumApi, chainConfig.ReadonlyNetwork, chainConfig.ManaTokenContract.Address, calldata, ct);
    }
}
