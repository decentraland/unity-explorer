using Cysharp.Threading.Tasks;
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
        private const string TRANSACTIONS_SERVER_URL_MAINNET = "https://transactions-api.decentraland.org/v1/transactions"; // Mainnet relay server (Polygon chainId=137)
        private const string TRANSACTIONS_SERVER_URL_TESTNET = "https://transactions-api.decentraland.zone/v1/transactions"; // Testnet relay server (Amoy chainId=80002)

        private const bool USE_MANUAL_EIP712_SIGNING = true;
        private const bool USE_RAW_HASH_SIGNING = false;

        /// <summary>
        ///     Known Decentraland contracts that support meta-transactions.
        ///     Key = lowercase contract address, Value = (name, version) for EIP-712 domain.
        ///     IMPORTANT: EIP-712 domain names must EXACTLY match what's in decentraland-transactions npm package!
        ///     See: https://github.com/decentraland/decentraland-transactions/blob/master/src/contracts/manaToken.ts
        /// </summary>
        private static readonly Dictionary<string, ContractMetaTxInfo> KnownMetaTxContracts = new ()
        {
            // MANA on Polygon Mainnet
            // EIP-712 name from manaToken.ts: "(PoS) Decentraland MANA"
            { "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4", new ContractMetaTxInfo("(PoS) Decentraland MANA", "1") },

            // MANA on Polygon Amoy Testnet (for testing ThirdWeb donation flow)
            // EIP-712 name from manaToken.ts: "Decentraland MANA(PoS)" (no space before parenthesis!)
            { "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0", new ContractMetaTxInfo("Decentraland MANA(PoS)", "1", 80002) },

            // Marketplace V2 on Polygon
            { "0x480a0f4e360e8964e68858dd231c2922f1df45ef", new ContractMetaTxInfo("Decentraland Marketplace", "2") },

            // Bids V2 on Polygon
            { "0xb96697fa4a3361ba35b774a42c58daccaad1b8e1", new ContractMetaTxInfo("Decentraland Bid", "2") },

            // Collection Manager on Polygon
            { "0x9d32aac179153a991e832550d9f96f6d1e05d4b4", new ContractMetaTxInfo("CollectionManager", "2") },
        };

        private readonly ThirdwebClient client;

        public ThirdWebMetaTxService(ThirdwebClient client)
        {
            this.client = client;
        }

        /// <summary>
        ///     Gets the appropriate relay server URL based on the contract's chainId.
        /// </summary>
        private static string GetRelayServerUrl(int chainId)
        {
            // Polygon mainnet uses .org, testnets use .zone
            bool isMainnet = chainId == 137;
            string url = isMainnet ? TRANSACTIONS_SERVER_URL_MAINNET : TRANSACTIONS_SERVER_URL_TESTNET;
            Debug.Log($"[ThirdWeb] Using relay server for chainId={chainId}: {url}");
            return url;
        }

        /// <summary>
        ///     Sends a meta-transaction via Decentraland's transactions-server.
        ///     The user signs an EIP-712 message, and the server relays the transaction paying for gas.
        /// </summary>
        public async UniTask<string> SendMetaTransactionAsync(IThirdwebWallet wallet, string contractAddress, string functionSignature)
        {
            string from = await wallet!.GetAddress();

            Debug.Log("[ThirdWeb] Sending meta-transaction via Decentraland relay");
            Debug.Log($"[ThirdWeb] From: {from}, Contract: {contractAddress}");
            Debug.Log($"[ThirdWeb] Function signature: {functionSignature}");

            // Get contract data for EIP-712 domain (includes chainId for the contract's network)
            ContractMetaTxInfo contractInfo = await GetContractMetaTxInfoAsync(contractAddress);
            int targetChainId = contractInfo.ChainId;

            Debug.Log($"[ThirdWeb] Contract info - Name: {contractInfo.Name}, Version: {contractInfo.Version}, ChainId: {targetChainId}");

            // 1. Get meta-tx nonce from the contract (must query the contract's chain RPC)
            BigInteger nonce = await GetMetaTxNonceAsync(contractAddress, from, targetChainId);
            Debug.Log($"[ThirdWeb] Meta-tx nonce: {nonce}");

            // Debug: Get and compare domain separator from contract
            string contractDomainSeparator = await GetContractDomainSeparatorAsync(contractAddress, targetChainId);
            string ourDomainSeparator = Web3Utils.ComputeDomainSeparator(contractInfo.Name, contractInfo.Version, contractAddress, targetChainId);
            Debug.Log($"[ThirdWeb] Contract's domainSeparator: {contractDomainSeparator}");
            Debug.Log($"[ThirdWeb] Our computed domainSeparator: {ourDomainSeparator}");

            if (!string.Equals(contractDomainSeparator, ourDomainSeparator, StringComparison.OrdinalIgnoreCase))
                Debug.LogError("[ThirdWeb] ❌ DOMAIN SEPARATOR MISMATCH! Contract uses different EIP-712 parameters.");

            string signature;

            if (USE_MANUAL_EIP712_SIGNING)
            {
                // Manual EIP-712: compute hash with Nethereum and sign via eth_sign (raw hash signing)
                signature = await SignMetaTxManuallyAsync(
                    wallet,
                    contractInfo.Name,
                    contractInfo.Version,
                    contractAddress,
                    targetChainId,
                    nonce,
                    from,
                    functionSignature
                );
            }
            else
            {
                // 3. Create EIP-712 typed data JSON for ThirdWeb SignTypedDataV4
                string typedDataJson = Web3Utils.CreateMetaTxTypedData(
                    contractInfo.Name,
                    contractInfo.Version,
                    contractAddress,
                    targetChainId,
                    nonce,
                    from,
                    functionSignature
                );

                Debug.Log($"[ThirdWeb] EIP-712 typed data:\n{typedDataJson}");

                // 4. Sign with ThirdWeb wallet
                signature = await wallet!.SignTypedDataV4(typedDataJson);
            }

            Debug.Log($"[ThirdWeb] Full signature: {signature}");
            Debug.Log($"[ThirdWeb] Signature length: {signature.Length}");

            // 5. Encode executeMetaTransaction call data
            string txData = Web3Utils.EncodeExecuteMetaTransaction(from, signature, functionSignature);
            Debug.Log($"[ThirdWeb] Encoded executeMetaTransaction: {txData[..50]}...");

            // 6. POST to transactions-server (use appropriate server based on chainId)
            return await PostToTransactionsServerAsync(from, contractAddress, txData, targetChainId);
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
            Debug.Log("[EIP712-Manual] ========== COMPUTING EIP-712 HASH WITH NETHEREUM ==========");

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

            Debug.Log($"[EIP712-Manual] TypedData JSON:\n{typedDataJson}");

            // Use Nethereum to encode the typed data and compute hash
            var signer = new Eip712TypedDataSigner();
            var nethereumHash = "";
            var manualHash = "";

            try
            {
                // Encode typed data using Nethereum
                byte[] encodedTypedData = signer.EncodeTypedData(typedDataJson);
                byte[] hashBytes = Sha3Keccack.Current.CalculateHash(encodedTypedData);
                nethereumHash = "0x" + BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                Debug.Log($"[EIP712-Manual] Nethereum encoded data length: {encodedTypedData.Length}");
                Debug.Log($"[EIP712-Manual] Nethereum computed HASH: {nethereumHash}");

                // Also compute hash manually for comparison
                manualHash = Web3Utils.ComputeEip712HashManually(contractName, contractVersion, contractAddress, chainIdValue, nonce, from, functionSignature);
                Debug.Log($"[EIP712-Manual] Manual computed HASH: {manualHash}");

                if (nethereumHash != manualHash)
                    Debug.LogWarning($"[EIP712-Manual] HASH MISMATCH! Nethereum: {nethereumHash}, Manual: {manualHash}");
                else
                    Debug.Log("[EIP712-Manual] Hashes match!");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EIP712-Manual] Nethereum EncodeTypedData failed: {e.Message}");
                Debug.LogWarning("[EIP712-Manual] This might be due to salt/chainId format difference.");

                // Fall back to manual hash
                manualHash = Web3Utils.ComputeEip712HashManually(contractName, contractVersion, contractAddress, chainIdValue, nonce, from, functionSignature);
                nethereumHash = manualHash;
                Debug.Log($"[EIP712-Manual] Using manual computed HASH: {manualHash}");
            }

            string signature;

            if (USE_RAW_HASH_SIGNING && !string.IsNullOrEmpty(manualHash))
            {
                // Alternative approach: Sign the raw hash directly
                // This bypasses ThirdWeb's SignTypedDataV4 implementation
                Debug.Log("[EIP712-Manual] Using RAW HASH SIGNING approach...");
                signature = await SignRawHashAsync(wallet, from, manualHash);
            }
            else
            {
                // Standard approach: Sign via ThirdWeb SignTypedDataV4
                Debug.Log("[EIP712-Manual] Signing via ThirdWeb SignTypedDataV4...");
                signature = await wallet!.SignTypedDataV4(typedDataJson);
            }

            Debug.Log($"[EIP712-Manual] Signature: {signature}");

            // Try to recover the signer address from the signature
            try
            {
                string recoveredAddress = signer.RecoverFromSignatureV4(typedDataJson, signature);
                Debug.Log($"[EIP712-Manual] Recovered address from signature: {recoveredAddress}");
                Debug.Log($"[EIP712-Manual] Expected signer address: {from}");

                if (recoveredAddress.Equals(from, StringComparison.OrdinalIgnoreCase))
                    Debug.Log("[EIP712-Manual] ✅ ADDRESSES MATCH! Signature is valid for our computed hash.");
                else
                    Debug.LogError("[EIP712-Manual] ❌ ADDRESSES DO NOT MATCH! ThirdWeb is hashing differently.");
            }
            catch (Exception e) { Debug.LogWarning($"[EIP712-Manual] Could not recover address: {e.Message}"); }

            return signature;
        }

        /// <summary>
        ///     Signs a raw 32-byte hash directly using the wallet's private key.
        ///     This bypasses EIP-712 typed data processing entirely.
        ///     NOTE: This uses PersonalSign which adds "\x19Ethereum Signed Message:\n32" prefix.
        ///     For true raw signing, we would need eth_sign or direct access to the private key.
        ///     The contract's ecrecover would need to expect this prefix.
        /// </summary>
        private async UniTask<string> SignRawHashAsync(IThirdwebWallet wallet, string signerAddress, string hashHex)
        {
            Debug.Log($"[EIP712-Manual] Signing raw hash: {hashHex}");

            // Convert hash to bytes
            byte[] hashBytes = Web3Utils.HexToBytes(hashHex);

            if (hashBytes.Length != 32)
            {
                Debug.LogError($"[EIP712-Manual] Hash must be 32 bytes, got {hashBytes.Length}");
                throw new Exception("Invalid hash length for signing");
            }

            // Try using PersonalSign with raw bytes
            // WARNING: PersonalSign adds "\x19Ethereum Signed Message:\n32" prefix!
            // This will NOT work directly with contract's ecrecover which expects EIP-712 format
            string signature = await wallet!.PersonalSign(hashBytes);

            Debug.Log($"[EIP712-Manual] Raw hash signature (with personal_sign prefix): {signature}");
            Debug.LogWarning("[EIP712-Manual] NOTE: PersonalSign adds message prefix - contract may not accept this!");

            return signature;
        }

        /// <summary>
        ///     Gets the domain separator from the contract for verification.
        ///     Tries both domainSeparator() and getDomainSeparator() selectors.
        /// </summary>
        private async UniTask<string> GetContractDomainSeparatorAsync(string contractAddress, int targetChainId)
        {
            // Try domainSeparator() first (0xf698da25)
            var request1 = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_call",
                @params = new object[]
                {
                    new { to = contractAddress, data = "0xf698da25" }, // domainSeparator()
                    "latest",
                },
            };

            try
            {
                EthApiResponse response = await SendRpcRequestAsync(request1, targetChainId);
                string result = response.result?.ToString() ?? "0x";

                if (!string.IsNullOrEmpty(result) && result != "0x" && result.Length > 2)
                    return result;
            }
            catch (Exception e) { Debug.Log($"[ThirdWeb] domainSeparator() failed: {e.Message}, trying getDomainSeparator()..."); }

            // Try getDomainSeparator() as fallback (0xed24911d)
            var request2 = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_call",
                @params = new object[]
                {
                    new { to = contractAddress, data = "0xed24911d" }, // getDomainSeparator()
                    "latest",
                },
            };

            try
            {
                EthApiResponse response = await SendRpcRequestAsync(request2, targetChainId);
                return response.result?.ToString() ?? "0x";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ThirdWeb] Failed to get domainSeparator from contract: {e.Message}");
                return "0x";
            }
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

            EthApiResponse response = await SendRpcRequestAsync(request, targetChainId);
            return Web3Utils.ParseHexToBigInteger(response.result?.ToString() ?? "0x0");
        }

        /// <summary>
        ///     Gets contract metadata needed for EIP-712 domain.
        ///     First tries known contracts (MANA, Marketplace, etc.), then uses DCL collection defaults.
        /// </summary>
        private UniTask<ContractMetaTxInfo> GetContractMetaTxInfoAsync(string contractAddress)
        {
            Debug.Log($"[ThirdWeb] GetContractMetaTxInfoAsync called for contract: {contractAddress}");

            string addressLower = contractAddress.ToLower();
            Debug.Log($"[ThirdWeb] Looking up contract in KnownMetaTxContracts with key: {addressLower}");

            // Check known Decentraland contracts first (MANA, Marketplace, Bid, CollectionManager)
            if (KnownMetaTxContracts.TryGetValue(addressLower, out ContractMetaTxInfo? known))
            {
                Debug.Log($"[ThirdWeb] ★ FOUND in KnownMetaTxContracts: Name='{known.Name}', Version='{known.Version}', ChainId={known.ChainId}");
                return UniTask.FromResult(known);
            }

            Debug.Log("[ThirdWeb] Contract NOT found in KnownMetaTxContracts, available keys:");

            foreach (string? key in KnownMetaTxContracts.Keys)
                Debug.Log($"[ThirdWeb]   - {key}");

            // For DCL wearable/emote collection contracts (ERC721BaseCollectionV2):
            // EIP-712 domain is HARDCODED in the contract, NOT from name() function!
            // See: ERC721BaseCollectionV2.initialize() calls _initializeEIP712('Decentraland Collection', '2')
            // The name() function returns the ERC721 token name, which is DIFFERENT from EIP-712 domain name.
            //
            // All DCL collection contracts use:
            //   - EIP712 name: "Decentraland Collection"
            //   - EIP712 version: "2"
            //   - ChainId: 137 (Polygon mainnet - DCL collections are only on Polygon mainnet)
            Debug.Log("[ThirdWeb] Using DEFAULT DCL collection EIP-712 domain: name='Decentraland Collection', version='2', chainId=137");
            return UniTask.FromResult(new ContractMetaTxInfo("Decentraland Collection", "2"));
        }

        /// <summary>
        ///     Calls name() on the contract to get its EIP-712 domain name.
        /// </summary>
        private async UniTask<string> GetContractNameAsync(string contractAddress, int targetChainId)
        {
            // name() selector = 0x06fdde03
            var request = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_call",
                @params = new object[]
                {
                    new { to = contractAddress, data = "0x06fdde03" },
                    "latest",
                },
            };

            EthApiResponse response = await SendRpcRequestAsync(request, targetChainId);
            var hex = response.result?.ToString();

            if (string.IsNullOrEmpty(hex) || hex == "0x")
                return string.Empty;

            return Web3Utils.DecodeStringFromHex(hex);
        }

        /// <summary>
        ///     POSTs the signed meta-transaction to Decentraland's transactions-server.
        ///     Based on: https://github.com/decentraland/decentraland-transactions/blob/master/src/sendMetaTransaction.ts
        /// </summary>
        private async UniTask<string> PostToTransactionsServerAsync(
            string from,
            string contractAddress,
            string txData,
            int chainId)
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
            string relayUrl = GetRelayServerUrl(chainId);
            Debug.Log($"[ThirdWeb] Posting to transactions-server ({relayUrl}):\n{payloadJson}");

            IThirdwebHttpClient httpClient = client.HttpClient;

            var content = new System.Net.Http.StringContent(
                payloadJson,
                Encoding.UTF8,
                "application/json"
            );

            ThirdwebHttpResponseMessage response = await httpClient.PostAsync(
                relayUrl,
                content,
                CancellationToken.None
            );

            string responseJson = await response.Content.ReadAsStringAsync();
            Debug.Log($"[ThirdWeb] Transactions-server response: {responseJson}");

            if (!response.IsSuccessStatusCode)
                throw new Web3Exception($"Meta-transaction relay failed: {response.StatusCode} - {responseJson}");

            TransactionsServerResponse? result = JsonConvert.DeserializeObject<TransactionsServerResponse>(responseJson);

            if (result == null || string.IsNullOrEmpty(result.txHash))
                throw new Web3Exception($"Meta-transaction relay returned empty txHash: {responseJson}");

            Debug.Log($"[ThirdWeb] Meta-transaction successful! TxHash: {result.txHash}");
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
