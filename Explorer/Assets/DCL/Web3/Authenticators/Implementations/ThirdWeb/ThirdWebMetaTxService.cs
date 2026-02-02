using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using Thirdweb;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public class ThirdWebMetaTxService
    {
        /// <summary>
        ///     Known Decentraland contracts that support meta-transactions.
        ///     Key = lowercase contract address, Value = (name, version) for EIP-712 domain.
        ///     IMPORTANT: EIP-712 domain names must EXACTLY match what's in decentraland-transactions npm package!
        ///     See: https://github.com/decentraland/decentraland-transactions/blob/master/src/contracts/manaToken.ts
        /// </summary>
        private static readonly Dictionary<string, ContractMetaTxInfo> KnownMetaTxContracts = new ()
        {
            { "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4", new ContractMetaTxInfo("(PoS) Decentraland MANA", "1") }, // MANA on Polygon Mainnet
            { "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0", new ContractMetaTxInfo("Decentraland MANA(PoS)", "1", 80002) }, // MANA on Polygon Amoy Testnet (for testing ThirdWeb donation flow)
            { "0x480a0f4e360e8964e68858dd231c2922f1df45ef", new ContractMetaTxInfo("Decentraland Marketplace", "2") }, // Marketplace V2 on Polygon
            { "0xb96697fa4a3361ba35b774a42c58daccaad1b8e1", new ContractMetaTxInfo("Decentraland Bid", "2") }, // Bids V2 on Polygon
            { "0x9d32aac179153a991e832550d9f96f6d1e05d4b4", new ContractMetaTxInfo("CollectionManager", "2") }, // Collection Manager on Polygon
        };

        private readonly ThirdwebClient client;
        private readonly URLDomain metaTxServerUrl;
        private readonly Func<EthApiRequest, int, UniTask<EthApiResponse>> sendRpcRequest;

        public ThirdWebMetaTxService(ThirdwebClient client, URLDomain metaTxServerUrl, Func<EthApiRequest, int, UniTask<EthApiResponse>> sendRpcRequest)
        {
            this.client = client;
            this.metaTxServerUrl = metaTxServerUrl;
            this.sendRpcRequest = sendRpcRequest;
        }

        /// <summary>
        ///     Sends a meta-transaction via Decentraland's transactions-server.
        ///     The user signs an EIP-712 message, and the server relays the transaction paying for gas.
        /// </summary>
        public async UniTask<string> SendMetaTransactionAsync(IThirdwebWallet wallet, string contractAddress, string functionSignature)
        {
            string from = await wallet!.GetAddress();

            // Get contract data for EIP-712 domain (includes chainId for the contract's network)
            ContractMetaTxInfo contractInfo = await GetContractMetaTxInfoAsync(contractAddress);
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"Contract info - Name: {contractInfo.Name}, Version: {contractInfo.Version}, ChainId: {contractInfo.ChainId}");

            BigInteger nonce = await GetMetaTxNonceAsync(contractAddress, from, contractInfo.ChainId);

            // Manual EIP-712: compute hash with Nethereum and sign via eth_sign (raw hash signing)
            string signature = await SignMetaTxManuallyAsync(
                wallet,
                contractInfo.Name,
                contractInfo.Version,
                contractAddress,
                contractInfo.ChainId,
                nonce,
                from,
                functionSignature
            );

            string txData = Web3Utils.EncodeExecuteMetaTransaction(from, signature, functionSignature);
            return await PostToTransactionsServerAsync(from, contractAddress, txData);
        }

        /// <summary>
        ///     Manually computes EIP-712 hash using Nethereum and verifies/debugs the signing process.
        ///     Can either sign via ThirdWeb SignTypedDataV4 or sign the raw hash directly.
        /// </summary>
        private async UniTask<string> SignMetaTxManuallyAsync(
            IThirdwebWallet wallet,
            string contractName,
            string contractVersion,
            string contractAddress,
            int chainIdValue,
            BigInteger nonce,
            string from,
            string functionSignature)
        {
            // Create TypedData using Nethereum EIP712
            // Note: DCL uses 'salt' instead of 'chainId' in domain (bytes32 vs uint256)
            string typedDataJson = Web3Utils.CreateMetaTxTypedData(
                contractName,
                contractVersion,
                contractAddress,
                chainIdValue,
                nonce,
                from,
                functionSignature
            );

            return await wallet!.SignTypedDataV4(typedDataJson);
        }

        /// <summary>
        ///     Gets the meta-transaction nonce for a user from the contract.
        ///     Calls getNonce(address) on the contract.
        /// </summary>
        private async UniTask<BigInteger> GetMetaTxNonceAsync(string contractAddress, string userAddress, int targetChainId)
        {
            // getNonce(address) selector = keccak256("getNonce(address)")[:4] = 0x2d0335ab
            string cleanAddress = userAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? userAddress[2..]
                : userAddress;

            string data = "0x2d0335ab" + cleanAddress.ToLower().PadLeft(64, '0');

            var request = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_call",
                @params = new object[]
                {
                    new { to = contractAddress, data },
                    "latest",
                },
            };

            EthApiResponse response = await sendRpcRequest(request, targetChainId);
            return Web3Utils.ParseHexToBigInteger(response.result?.ToString() ?? "0x0");
        }

        /// <summary>
        ///     Gets contract metadata needed for EIP-712 domain.
        ///     First tries known contracts (MANA, Marketplace, etc.), then uses DCL collection defaults.
        ///
        /// For DCL wearable/emote collection contracts (ERC721BaseCollectionV2):
        /// EIP-712 domain is HARDCODED in the contract, NOT from name() function!
        /// See: ERC721BaseCollectionV2.initialize() calls _initializeEIP712('Decentraland Collection', '2')
        /// The name() function returns the ERC721 token name, which is DIFFERENT from EIP-712 domain name.
        ///
        /// All DCL collection contracts use:
        ///   - EIP712 name: "Decentraland Collection"
        ///   - EIP712 version: "2"
        ///   - ChainId: 137 (Polygon mainnet - DCL collections are only on Polygon mainnet)
        /// </summary>
        private UniTask<ContractMetaTxInfo> GetContractMetaTxInfoAsync(string contractAddress)
        {
            string addressLower = contractAddress.ToLower();

            // Check known Decentraland contracts first (MANA, Marketplace, Bid, CollectionManager)
            if (KnownMetaTxContracts.TryGetValue(addressLower, out ContractMetaTxInfo? known))
                return UniTask.FromResult(known);

            ReportHub.LogError(ReportCategory.AUTHENTICATION, $"Contract NOT found in {nameof(KnownMetaTxContracts)}. Using DEFAULT DCL collection EIP-712 domain");
            return UniTask.FromResult(new ContractMetaTxInfo("Decentraland Collection", "2"));
        }

        /// <summary>
        ///     POSTs the signed meta-transaction to Decentraland's transactions-server.
        ///     Based on: https://github.com/decentraland/decentraland-transactions/blob/master/src/sendMetaTransaction.ts
        /// </summary>
        private async UniTask<string> PostToTransactionsServerAsync(
            string from,
            string contractAddress,
            string txData)
        {
            // Use addresses as-is (like JS library does)
            // Format expected by transactions-server:
            // { transactionData: { from, params: [contractAddress, txData] } }
            var payload = new
            {
                transactionData = new
                {
                    from,
                    @params = new[] { contractAddress, txData },
                },
            };

            string payloadJson = JsonConvert.SerializeObject(payload);

            IThirdwebHttpClient httpClient = client.HttpClient;

            var content = new System.Net.Http.StringContent(
                payloadJson,
                Encoding.UTF8,
                "application/json"
            );

            ThirdwebHttpResponseMessage response = await httpClient.PostAsync(
                metaTxServerUrl.Value,
                content,
                CancellationToken.None
            );

            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Web3Exception($"Meta-transaction relay failed: {response.StatusCode} - {responseJson}");

            TransactionsServerResponse? result = JsonConvert.DeserializeObject<TransactionsServerResponse>(responseJson);

            if (result == null || string.IsNullOrEmpty(result.txHash))
                throw new Web3Exception($"Meta-transaction relay returned empty txHash: {responseJson}");

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb Meta-transaction successful! TxHash: {result.txHash}");
            return result.txHash;
        }

        /// <summary>
        ///     Contract metadata for EIP-712 domain.
        ///     ChainId is the chain where the contract is deployed (used for EIP-712 salt and RPC queries).
        /// </summary>
        private record ContractMetaTxInfo(string Name, string Version, int ChainId = 137);

        private class TransactionsServerResponse
        {
            public string? txHash { get; set; }
            public string? error { get; set; }
        }
    }
}
