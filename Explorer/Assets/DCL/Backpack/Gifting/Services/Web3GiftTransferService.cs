using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3;
using System;
using System.Threading;
using DCL.Backpack.Gifting.Utils;
using Newtonsoft.Json.Linq;

namespace DCL.Backpack.Gifting.Services
{
    /// <summary>
    ///     Implements the gift transfer functionality using a gasless meta-transaction flow.
    ///     The user signs a typed message (EIP-712), and a backend relayer submits the transaction,
    ///     paying the gas fee on the user's behalf.
    /// </summary>
    public class Web3GiftTransferService : IGiftTransferService, IDisposable
    {
        private readonly IEthereumApi ethereumApi;

        public Web3GiftTransferService(IEthereumApi ethereumApi)
        {
            this.ethereumApi = ethereumApi;
        }

        public void Dispose() { }

        /// <summary>
        ///     Requests a gasless transfer of a gift (NFT) to a recipient.
        ///     This will trigger a browser pop-up for the user to sign a typed message.
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <param name="giftUrn">The URN of the NFT to be transferred.</param>
        /// <param name="tokenId"></param>
        /// <param name="recipientAddress">The Ethereum address of the recipient.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A GiftTransferResult indicating success or failure.</returns>
        public async UniTask<GiftTransferResult> RequestTransferAsync(string fromAddress,
            string giftUrn,
            string tokenId,
            string recipientAddress,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(fromAddress))
                    return GiftTransferResult.Fail("Web3 identity not found. Please ensure the user is logged in.");

                if (!GiftingUrnParsingHelper.TryGetContractAddress(giftUrn, out string contractAddress))
                    return GiftTransferResult.Fail($"Could not extract contract address from URN: {giftUrn}");

                // Build call data for transferFrom(from, to, tokenId)
                string data = ManualTxEncoder.EncodeTransferFrom(fromAddress, recipientAddress, tokenId);

                // Compose tx: ONLY from, to, data
                var tx = new JObject
                {
                    ["from"] = fromAddress, ["to"]   = contractAddress, ["data"] = data
                };

                var request = new EthApiRequest
                {
                    id = Guid.NewGuid().GetHashCode(), method = "eth_sendTransaction", @params = new object[]
                    {
                        tx
                    }
                };

                // This call automatically triggers the browser pop-up via DappWeb3Authenticator
                var response = await ethereumApi.SendAsync(request, ct);

                if (response.result == null ||
                    string.IsNullOrEmpty(response.result.ToString()))
                    
                    return GiftTransferResult.Fail("Signing failed. The user may have rejected the request.");

                string signature = response.result.ToString();
                ReportHub.Log(ReportCategory.GIFTING, $"Gifting message signed successfully. Signature: {signature.Substring(0, 10)}...");

                return GiftTransferResult.Success();
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
                return GiftTransferResult.Fail(e.Message);
            }
        }
    }
}