using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3;
using DCL.Web3.Identities;

namespace DCL.Backpack.Gifting.Services
{
    public class Web3GiftTransferService : IGiftTransferService, IDisposable
    {
        public event Action<int, DateTime> OnVerificationCodeReceived;

        private readonly IVerifiedEthereumApi verifiedEthereumApi;
        private readonly IWeb3IdentityCache identityCache;

        public Web3GiftTransferService(IVerifiedEthereumApi verifiedEthereumApi, IWeb3IdentityCache identityCache)
        {
            this.verifiedEthereumApi = verifiedEthereumApi;
            this.identityCache = identityCache;
            this.verifiedEthereumApi.AddVerificationListener(HandleVerificationCode);
        }

        public void Dispose()
        {
            verifiedEthereumApi.AddVerificationListener(null);
        }

        private void HandleVerificationCode(int code, DateTime expiration)
        {
            OnVerificationCodeReceived?.Invoke(code, expiration);
        }

        public async UniTask<GiftTransferResult> RequestTransferAsync(string giftUrn, string recipientAddress, CancellationToken ct)
        {
            // The URN needs to be parsed to get the contract address and token ID.
            // Example URN: "urn:decentraland:ethereum:collections-v1:halloween_2019:razor_blade_feet"
            // The backend is the ultimate authority on this parsing, but this is the logic.
            if (!TryParseUrn(giftUrn, out string contractAddress, out string tokenId))
            {
                ReportHub.LogError(ReportCategory.GIFTING, $"Could not parse URN: {giftUrn}");
                return GiftTransferResult.Fail("Invalid item URN format.");
            }

            // The backend MUST create the `data` payload. It's a hex string representing the function call.
            // It will look something like this: "0x23b872dd" + [padded fromAddress] + [padded toAddress] + [padded tokenId]
            // Your client should NOT be responsible for creating this.
            // You will send the raw data to the backend, and it will construct the final transaction.
            var requestPayload = new
            {
                from = identityCache.Identity.Address.ToString(), to = recipientAddress,   tokenId
            };

            // This is the object that will be sent to the backend. The backend then constructs the full transaction.
            var request = new EthApiRequest
            {
                id = 1, method = "eth_sendTransaction",
                // The params will contain the information the backend needs to build the transaction.
                // The exact structure MUST be agreed upon with the backend team.
                // This is a likely structure:
                @params = new object[]
                {
                    new
                    {
                        to = contractAddress, // The NFT contract address
                        // The backend will take the payload below and encode it into the `data` field.
                        payload = requestPayload
                    }
                }
            };

            try
            {
                ReportHub.Log(ReportCategory.GIFTING, $"Sending gift transfer request for URN: {giftUrn} to {recipientAddress}");
                var response = await verifiedEthereumApi.SendAsync(request, ct);

                if (response.result != null && !string.IsNullOrEmpty(response.result.ToString()))
                {
                    ReportHub.Log(ReportCategory.GIFTING, $"Gift transfer transaction successfully broadcasted. Hash: {response.result}");
                    return GiftTransferResult.Success();
                }

                ReportHub.LogWarning(ReportCategory.GIFTING, "Gift transfer API returned a null or empty result.");
                return GiftTransferResult.Fail("Transaction failed: received an empty response.");
            }
            // ... (keep the same catch blocks as the previous version)
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.GIFTING);
                return GiftTransferResult.Fail("An unexpected error occurred.");
            }
        }

        // A helper function to extract data from the URN.
        private bool TryParseUrn(string urn, out string contractAddress, out string tokenId)
        {
            contractAddress = null;
            tokenId = null;
            string[]? parts = urn.Split(':');

            // Example: urn:decentraland:ethereum:collections-v1:halloween_2019:razor_blade_feet
            if (parts.Length < 6 || parts[2] != "ethereum")
                return false;

            // The contract address is part 4
            contractAddress = parts[4];
            // The token ID (or name that resolves to an ID) is part 5
            tokenId = parts[5];

            // You may need to look up the contract address from a known list if it's a collection name
            // e.g., "halloween_2019" -> "0xc1f4b0eea2bd6690930e6c66efd3e197d620b9c2"
            // The backend should ideally handle this translation.
            return true;
        }
    }
}