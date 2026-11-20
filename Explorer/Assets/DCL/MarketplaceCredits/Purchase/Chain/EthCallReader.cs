using Cysharp.Threading.Tasks;
using DCL.Web3;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    public static class EthCallReader
    {
        private const string ETH_CALL_METHOD = "eth_call";
        private const string LATEST_BLOCK = "latest";

        /// <summary>
        ///     Runs a read-only call that returns one uint256. An empty result reads as zero; throws when the node
        ///     gives no result at all.
        /// </summary>
        public static async UniTask<BigInteger> ReadUint256Async(IEthereumApi ethereumApi, string readonlyNetwork, string contractAddress, string calldata, CancellationToken ct)
        {
            var request = new EthApiRequest
            {
                readonlyNetwork = readonlyNetwork,
                id = Guid.NewGuid().GetHashCode(),
                method = ETH_CALL_METHOD,
                @params = new object[]
                {
                    new JObject
                    {
                        ["to"] = contractAddress,
                        ["data"] = calldata,
                    },
                    LATEST_BLOCK,
                },
            };

            EthApiResponse response = await ethereumApi.SendAsync(request, Web3RequestSource.Internal, ct);
            string hex = response.result?.ToString() ?? throw new InvalidOperationException($"eth_call to {contractAddress} returned no result");
            string digits = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            if (digits.Length == 0)
                return BigInteger.Zero;

            // The leading zero keeps a value whose top bit is set from being parsed as negative.
            return BigInteger.Parse($"0{digits}", NumberStyles.HexNumber);
        }
    }
}
