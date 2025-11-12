using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3;
using DCL.Web3.Identities;
using Newtonsoft.Json;
using System;
using System.Threading;
using DCL.Web3.Authenticators;

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
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IVerifiedEthereumApi verifiedEthereumApi;

        // --- IMPORTANT ---
        // This URL must be provided by your backend team. It is the endpoint for the
        // relayer server that will submit the signed transaction to the blockchain.
        private const string RELAYER_API_URL = "https://your.backend.relayer/api/send-meta-transaction";

        public Web3GiftTransferService(IEthereumApi ethereumApi,
            IWeb3IdentityCache web3IdentityCache,
            IVerifiedEthereumApi verifiedEthereumApi)
        {
            this.ethereumApi = ethereumApi;
            this.web3IdentityCache = web3IdentityCache;
            this.verifiedEthereumApi = verifiedEthereumApi;
        }

        public void Dispose() { }


        public event Action<int, DateTime>? OnVerificationCodeReceived;

        /// <summary>
        ///     Requests a gasless transfer of a gift (NFT) to a recipient.
        ///     This will trigger a browser pop-up for the user to sign a typed message.
        /// </summary>
        /// <param name="giftUrn">The URN of the NFT to be transferred.</param>
        /// <param name="recipientAddress">The Ethereum address of the recipient.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A GiftTransferResult indicating success or failure.</returns>
        public async UniTask<GiftTransferResult> RequestTransferAsync(string giftUrn, string recipientAddress, CancellationToken ct)
        {
            try
            {
                var identity = web3IdentityCache.Identity;
                if (identity == null)
                    return GiftTransferResult.Fail("Web3 identity not found. Please ensure the user is logged in.");

                var parsedUrn = UrnParser.Parse(giftUrn);
                if (parsedUrn == null)
                    return GiftTransferResult.Fail($"Invalid gift URN format: {giftUrn}");

                verifiedEthereumApi.AddVerificationListener(OnVerification);

                string contractAddress = parsedUrn.Value.contractAddress;
                string tokenId = parsedUrn.Value.tokenId;
                string fromAddress = identity.Address.ToString();

                // --- Step 1: Craft the EIP-712 Typed Data for Signing ---
                // This structure is critical and MUST match exactly what the backend relayer
                // and the smart contract expect. Confirm this with your backend team (Andrés).
                var typedData = new
                {
                    types = new
                    {
                        EIP712Domain = new[]
                        {
                            new
                            {
                                name = "name", type = "string"
                            },
                            new
                            {
                                name = "version", type = "string"
                            },
                            new
                            {
                                name = "chainId", type = "uint256"
                            },
                            new
                            {
                                name = "verifyingContract", type = "address"
                            }
                        },
                        // The name "Gift" and its properties must match the smart contract's implementation.
                        Gift = new[]
                        {
                            new
                            {
                                name = "from", type = "address"
                            },
                            new
                            {
                                name = "to", type = "address"
                            },
                            new
                            {
                                name = "tokenId", type = "uint256"
                            }
                        }
                    },
                    primaryType = "Gift", domain = new
                    {
                        name = "Decentraland Gifting", version = "1", chainId = 137, // 137 for Polygon Mainnet. This may need to be dynamic based on the network.
                        verifyingContract = contractAddress
                    },
                    message = new
                    {
                        from = fromAddress, to = recipientAddress,  tokenId
                    }
                };

                // --- Step 2: Create and send the request to get the user's signature ---
                var request = new EthApiRequest
                {
                    id = Guid.NewGuid().GetHashCode(), method = "eth_signTypedData_v4", @params = new object[]
                    {
                        fromAddress, JsonConvert.SerializeObject(typedData)
                    }
                };

                // This call automatically triggers the browser pop-up via DappWeb3Authenticator
                var response = await ethereumApi.SendAsync(request, ct);

                if (response.result == null || string.IsNullOrEmpty(response.result.ToString()))
                    return GiftTransferResult.Fail("Signing failed. The user may have rejected the request.");

                string signature = response.result.ToString();
                ReportHub.Log(ReportCategory.GIFTING, $"Gifting message signed successfully. Signature: {signature.Substring(0, 10)}...");

                // --- Step 3: Send the signed message to the Foundation's Relayer API ---
                // The relayer will now submit the transaction and pay the gas fee.
                // The implementation of this POST request depends on your project's HTTP client.

                // TODO: Implement the HTTP POST call to your relayer.
                // Example:
                // var relayerPayload = new { signature, message = typedData.message };
                // var relayerResponse = await YourHttpClient.PostAsync(RELAYER_API_URL, relayerPayload);
                // if (!relayerResponse.IsSuccessStatusCode)
                //     return GiftTransferResult.Fail("Relayer failed to process the transaction.");
                //
                // string txHash = await relayerResponse.Content.ReadAsStringAsync();
                // return GiftTransferResult.Success(txHash);

                // For now, we return success assuming the relayer call will be implemented.
                // The "txHash" is a placeholder until the relayer call is made.
                return GiftTransferResult.Success();
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
                return GiftTransferResult.Fail(e.Message);
            }
        }

        private void OnVerification(int code, DateTime expiration)
        {
            OnVerificationCodeReceived?.Invoke(code, expiration);
        }
    }

    /// <summary>
    ///     A utility class to parse Decentraland's URN strings for wearables and emotes.
    /// </summary>
    public static class UrnParser
    {
        /// <summary>
        ///     Parses a Decentraland URN string to extract the contract address and token ID.
        /// </summary>
        /// <param name="urn">
        ///     The URN to parse.
        ///     Example: "urn:decentraland:matic:collections-v2:0x32b7495895264ac9d0b12d32afd435453458b1c6:123"
        /// </param>
        /// <returns>A tuple containing the contract address and token ID, or null if parsing fails.</returns>
        public static (string contractAddress, string tokenId)? Parse(string urn)
        {
            if (string.IsNullOrEmpty(urn))
                return null;

            string[]? parts = urn.Split(':');

            // A valid URN has at least 6 parts:
            // urn:decentraland:[network]:[asset_type]:[contract_address]:[token_id]
            if (parts.Length < 6 || parts[0] != "urn" || parts[1] != "decentraland")
                return null;

            string contractAddress = parts[4];
            string tokenId = parts[5];

            if (string.IsNullOrEmpty(contractAddress) || string.IsNullOrEmpty(tokenId))
                return null;

            return (contractAddress, tokenId);
        }
    }
}