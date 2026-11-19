using Cysharp.Threading.Tasks;
using DCL.Web3;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
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
        private const string ETH_CALL_METHOD = "eth_call";
        private const string LATEST_BLOCK = "latest";

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

        private async UniTask<BigInteger> CallAsync(string calldata, CancellationToken ct)
        {
            var request = new EthApiRequest
            {
                readonlyNetwork = chainConfig.ReadonlyNetwork,
                id = Guid.NewGuid().GetHashCode(),
                method = ETH_CALL_METHOD,
                @params = new object[]
                {
                    new JObject
                    {
                        ["to"] = chainConfig.ManaTokenContract.Address,
                        ["data"] = calldata,
                    },
                    LATEST_BLOCK,
                },
            };

            EthApiResponse response = await ethereumApi.SendAsync(request, Web3RequestSource.Internal, ct);
            string hex = response.result?.ToString() ?? throw new InvalidOperationException("MANA token read returned no result");
            string digits = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            if (digits.Length == 0)
                return BigInteger.Zero;

            // The leading zero keeps a value whose top bit is set from being parsed as negative.
            return BigInteger.Parse($"0{digits}", NumberStyles.HexNumber);
        }
    }
}
