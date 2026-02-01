using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
    public class ThirdWebEthereumApi
    {
        // Minimal ABI with a dummy function to create a base transaction that we'll customize
        // This allows us to support ANY contract call by overriding the data field
        private const string MINIMAL_ABI = @"[{""name"":""execute"",""type"":""function"",""inputs"":[],""outputs"":[]}]";

        private readonly ThirdwebClient client;

        private readonly HashSet<string> whitelistMethods;
        private readonly HashSet<string> readOnlyMethods;
        private readonly BigInteger chainId;

        public TransactionConfirmationDelegate? TransactionConfirmationCallback { private get; set; }

        private readonly SemaphoreSlim mutex = new (1, 1);
        private readonly ThirdWebMetaTxService metaTxService;

        public ThirdWebEthereumApi(ThirdwebClient client, HashSet<string> whitelistMethods, HashSet<string> readOnlyMethods, DecentralandEnvironment environment)
        {
            this.client = client;
            this.whitelistMethods = whitelistMethods;
            this.readOnlyMethods = readOnlyMethods;

            chainId = ChainUtils.GetChainIdAsInt(environment);

            metaTxService = new ThirdWebMetaTxService(client, SendRpcRequestAsync);
        }

        private static string GetRpcUrl(int chainId) =>
            $"https://{chainId}.rpc.thirdweb.com";

        public async UniTask<EthApiResponse> SendAsync(IThirdwebWallet wallet, EthApiRequest request, Web3RequestSource source, CancellationToken ct)
        {
            await mutex.WaitAsync(ct);
            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                if (!whitelistMethods.Contains(request.method))
                {
                    ReportHub.LogError(ReportCategory.AUTHENTICATION, $"ThirdWeb web3 operation: Method not allowed : {request.method}");
                    throw new Web3Exception($"The method is not allowed: {request.method}");
                }

                bool isReadOnly = IsReadOnly(request);

                if (isReadOnly)
                    return await SendWithoutConfirmationAsync(wallet, request, ct);

                return await SendWithConfirmationAsync(wallet, request, source, ct);
            }
            finally
            {
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext, CancellationToken.None);
                else
                    await UniTask.SwitchToMainThread(CancellationToken.None);

                mutex.Release();
            }
        }

        private bool IsReadOnly(EthApiRequest request)
        {
            foreach (string method in readOnlyMethods)
                if (string.Equals(method, request.method, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private async UniTask<EthApiResponse> SendWithoutConfirmationAsync(IThirdwebWallet wallet, EthApiRequest request, CancellationToken ct)
        {
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb web3 operation: Request method={request.method}, readonlyNetwork={request.readonlyNetwork ?? "null"}");

            // Determine target chainId: use readonlyNetwork if specified, otherwise use wallet's current chainId
            int? networkChainId = ChainUtils.GetChainIdFromReadonlyNetwork(request.readonlyNetwork);
            int targetChainId = networkChainId ?? (int)chainId;

            // eth_getBalance - can be handled locally for the active wallet (only if same chain)
            if (string.Equals(request.method, "eth_getBalance") && targetChainId == (int)chainId)
            {
                var address = request.@params[0].ToString();
                string walletAddress = await wallet!.GetAddress();

                if (string.Equals(address, walletAddress, StringComparison.OrdinalIgnoreCase))
                {
                    BigInteger balance = await wallet!.GetBalance(chainId);

                    return new EthApiResponse
                    {
                        id = request.id,
                        jsonrpc = "2.0",
                        result = "0x" + balance.ToString("x"),
                    };
                }
            }

            // Use targetChainId which respects readonlyNetwork for cross-chain queries (e.g., Polygon balance check)
            return await SendRpcRequestAsync(request, targetChainId);
        }

        // low-level calls
        private async UniTask<EthApiResponse> SendRpcRequestAsync(EthApiRequest request) =>
            await SendRpcRequestAsync(request, (int)chainId);

        private async UniTask<EthApiResponse> SendRpcRequestAsync(EthApiRequest request, int targetChainId)
        {
            string rpcUrl = GetRpcUrl(targetChainId);

            var rpcRequest = new
            {
                jsonrpc = "2.0",
                request.id,
                request.method,
                request.@params,
            };

            string requestJson = JsonConvert.SerializeObject(rpcRequest);

            IThirdwebHttpClient? httpClient = client.HttpClient;

            var content = new System.Net.Http.StringContent(
                requestJson,
                Encoding.UTF8,
                "application/json"
            );

            ThirdwebHttpResponseMessage? httpResponse = await httpClient.PostAsync(rpcUrl, content, CancellationToken.None);
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb HTTP Response status: {httpResponse.StatusCode}, IsSuccess={httpResponse.IsSuccessStatusCode}");

            if (!httpResponse.IsSuccessStatusCode)
            {
                string errorText = await httpResponse.Content.ReadAsStringAsync();
                throw new Web3Exception($"RPC request failed: {httpResponse.StatusCode} - {errorText}");
            }

            string responseJson = await httpResponse.Content.ReadAsStringAsync();
            EthApiResponse rpcResponse = JsonConvert.DeserializeObject<EthApiResponse>(responseJson);

            return new EthApiResponse
            {
                id = request.id,
                jsonrpc = "2.0",
                result = rpcResponse.result,
            };
        }

        /// <summary>
        ///     Handles methods that require user confirmation (signing, transactions)
        /// </summary>
        private async UniTask<EthApiResponse> SendWithConfirmationAsync(IThirdwebWallet wallet, EthApiRequest request, Web3RequestSource source, CancellationToken ct)
        {
            // Request user confirmation before proceeding
            if (TransactionConfirmationCallback != null)
            {
                TransactionConfirmationRequest confirmationRequest = await CreateConfirmationRequestAsync(wallet, request);

                // For Internal requests (Gifting, Donations, etc.), hide description and details panel
                // since they are already displayed in the feature-specific UI
                if (source == Web3RequestSource.Internal)
                {
                    confirmationRequest.HideDescription = true;
                    confirmationRequest.HideDetailsPanel = true;
                }

                bool confirmed = await TransactionConfirmationCallback(confirmationRequest);

                if (!confirmed)
                    throw new Web3Exception("Transaction rejected by user");
            }

            // Wallet signing methods
            if (string.Equals(request.method, "personal_sign"))
            {
                // personal_sign params: [message, address]
                var message = request.@params[0].ToString();
                string signature = await wallet!.PersonalSign(message);

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = signature,
                };
            }

            if (string.Equals(request.method, "eth_signTypedData_v4"))
            {
                // eth_signTypedData_v4 params: [address, typedData]
                var typedDataJson = request.@params[1].ToString();
                string signature = await wallet!.SignTypedDataV4(typedDataJson);

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = signature,
                };
            }

            if (string.Equals(request.method, "eth_sendTransaction"))
            {
                // Internal transactions (gifting, donations) use meta-transactions via Decentraland relay
                bool useMetaTx = source == Web3RequestSource.Internal;
                return await HandleSendTransactionAsync(wallet, request, useMetaTx);
            }

            // Fallback for any other non-read-only methods
            throw new Web3Exception($"Unsupported method requiring confirmation: {request.method}");
        }

        /// <summary>
        ///     Creates a confirmation request object with transaction details for the UI
        /// </summary>
        private async UniTask<TransactionConfirmationRequest> CreateConfirmationRequestAsync(IThirdwebWallet wallet, EthApiRequest request)
        {
            var confirmationRequest = new TransactionConfirmationRequest
            {
                Method = request.method,
                Params = request.@params,
                ChainId = (int)chainId,
                NetworkName = ChainUtils.GetNetworkNameById((int)chainId),
            };

            // Extract additional details for eth_sendTransaction
            if (string.Equals(request.method, "eth_sendTransaction") && request.@params?.Length > 0)
            {
                try
                {
                    Dictionary<string, object>? txParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.@params[0].ToString());
                    confirmationRequest.To = txParams?.TryGetValue("to", out object? toValue) == true ? toValue?.ToString() : null;
                    confirmationRequest.Value = txParams?.TryGetValue("value", out object? valueValue) == true ? valueValue?.ToString() : null;
                    confirmationRequest.Data = txParams?.TryGetValue("data", out object? dataValue) == true ? dataValue?.ToString() : null;
                }
                catch
                { /* Ignore parsing errors, UI will show what it can */
                }

                // Best-effort: balance + estimated gas fee (should never block the tx flow if it fails)
                try
                {
                    BigInteger balanceWei = await wallet!.GetBalance(chainId);
                    confirmationRequest.BalanceEth = balanceWei.ToString().ToEth(decimalsToDisplay: 6, addCommas: false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ThirdWeb] Failed to fetch balance for confirmation popup: {e.Message}");
                    confirmationRequest.BalanceEth = "0.0";
                }

                try
                {
                    // Re-parse to build txObject for estimateGas
                    Dictionary<string, object>? txParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.@params[0].ToString());
                    string? to = txParams?.TryGetValue("to", out object? toValue) == true ? toValue?.ToString() : null;
                    string value = txParams?.TryGetValue("value", out object? valueValue) == true ? valueValue?.ToString() ?? "0x0" : "0x0";
                    string data = txParams?.TryGetValue("data", out object? dataValue) == true ? dataValue?.ToString() ?? "0x" : "0x";

                    string from = await wallet!.GetAddress();

                    var txObject = new
                    {
                        from,
                        to,
                        value,
                        data,
                    };

                    // eth_estimateGas
                    var estimateGasRequest = new EthApiRequest
                    {
                        id = request.id,
                        method = "eth_estimateGas",
                        @params = new object[] { txObject },
                    };

                    EthApiResponse estimateGasResponse = await SendRpcRequestAsync(estimateGasRequest);
                    string gasLimitHex = estimateGasResponse.result?.ToString() ?? "0x0";
                    BigInteger gasLimit = gasLimitHex.HexToNumber();

                    // eth_gasPrice
                    var gasPriceRequest = new EthApiRequest
                    {
                        id = request.id,
                        method = "eth_gasPrice",
                        @params = Array.Empty<object>(),
                    };

                    EthApiResponse gasPriceResponse = await SendRpcRequestAsync(gasPriceRequest);
                    string gasPriceHex = gasPriceResponse.result?.ToString() ?? "0x0";
                    BigInteger gasPriceWei = gasPriceHex.HexToNumber();

                    BigInteger feeWei = gasLimit * gasPriceWei;
                    confirmationRequest.EstimatedGasFeeEth = feeWei.ToString().ToEth(decimalsToDisplay: 6, addCommas: false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ThirdWeb] Failed to estimate gas fee for confirmation popup: {e.Message}");
                    confirmationRequest.EstimatedGasFeeEth = "0.0";
                }
            }

            return confirmationRequest;
        }

        private async UniTask<EthApiResponse> HandleSendTransactionAsync(IThirdwebWallet wallet, EthApiRequest request, bool useMetaTx = false)
        {
            Debug.Log($"[ThirdWeb] HandleSendTransactionAsync called, useMetaTx={useMetaTx}");

            if (wallet == null)
            {
                Debug.LogError("[ThirdWeb] No active wallet connected!");
                throw new Web3Exception("No active wallet connected");
            }

            // eth_sendTransaction params: [transactionObject]
            var paramsJson = request.@params[0].ToString();
            Debug.Log($"[ThirdWeb] Transaction params JSON: {paramsJson}");

            Dictionary<string, object>? txParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(paramsJson);

            string? to = txParams?.TryGetValue("to", out object? toValue) == true ? toValue?.ToString() : null;
            string data = txParams?.TryGetValue("data", out object? dataValue) == true ? dataValue?.ToString() ?? "0x" : "0x";
            string value = txParams?.TryGetValue("value", out object? valueValue) == true ? valueValue?.ToString() ?? "0x0" : "0x0";

            Debug.Log($"[ThirdWeb] Parsed tx: to={to}, data={data?.Substring(0, Math.Min(50, data?.Length ?? 0))}..., value={value}");

            if (string.IsNullOrEmpty(to))
            {
                Debug.LogError("[ThirdWeb] eth_sendTransaction requires 'to' address!");
                throw new Web3Exception("eth_sendTransaction requires 'to' address");
            }

            // Parse value
            BigInteger weiValue = Web3Utils.ParseHexToBigInteger(value);
            Debug.Log($"[ThirdWeb] Parsed wei value: {weiValue}");

            // For meta-transactions (internal ops like gifting), use Decentraland relay
            // The user signs an EIP-712 message, and the relay pays for gas
            if (useMetaTx && !string.IsNullOrEmpty(data) && data != "0x")
            {
                Debug.Log($"[ThirdWeb] ★ Using meta-transaction for contract call to {to}");
                Debug.Log($"[ThirdWeb] Full data length: {data.Length} chars");
                string txHash = await metaTxService.SendMetaTransactionAsync(wallet, to, data);

                Debug.Log($"[ThirdWeb] Meta-transaction completed with txHash={txHash}");

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = txHash,
                };
            }

            // For simple ETH transfers (no data), use Transfer method
            if (string.IsNullOrEmpty(data) || data == "0x")
            {
                ThirdwebTransactionReceipt? txReceipt = await wallet!.Transfer(
                    chainId,
                    to,
                    weiValue
                );

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = txReceipt.TransactionHash,
                };
            }

            // For contract interactions, decode the data and use ThirdwebContract.Prepare with proper ABI
            // This is the recommended approach by Thirdweb SDK
            string hash = await ExecuteContractCallAsync(wallet, to, data, weiValue);

            return new EthApiResponse
            {
                id = request.id,
                jsonrpc = "2.0",
                result = hash,
            };
        }

        /// <summary>
        ///     Executes a contract call with pre-encoded data.
        ///     Creates a base transaction using a minimal ABI, then overrides the data field
        ///     with the actual encoded calldata. This supports ANY contract call.
        /// </summary>
        private async UniTask<string> ExecuteContractCallAsync(IThirdwebWallet wallet, string contractAddress, string data, BigInteger weiValue)
        {
            // Create contract with minimal ABI containing a dummy function
            ThirdwebContract contract = await ThirdwebContract.Create(
                client,
                contractAddress,
                chainId,
                MINIMAL_ABI
            );

            // Create a base transaction using the dummy function
            ThirdwebTransaction transaction = await ThirdwebContract.Prepare(
                wallet,
                contract,
                "execute",
                weiValue
            );

            // Override the data field with the actual pre-encoded calldata
            // This allows us to support ANY contract function, not just predefined ones
            transaction = transaction.SetData(data);

            return await ThirdwebTransaction.Send(transaction);
        }
    }
}
