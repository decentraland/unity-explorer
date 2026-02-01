using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Prefs;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;
using Thirdweb;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public class ThirdWebAuthenticator : IWeb3Authenticator, IEthereumApi, IOtpAuthenticator
    {
        private const string CLIENT_ID = "e1adce863fe287bb6cf0e3fd90bdb77f";
        private const string BUNDLE_ID = "com.Decentraland.Explorer";
        private const string SDK_VERSION = "6.0.5";

        // Minimal ABI with a dummy function to create a base transaction that we'll customize
        // This allows us to support ANY contract call by overriding the data field
        private const string MINIMAL_ABI = @"[{""name"":""execute"",""type"":""function"",""inputs"":[],""outputs"":[]}]";

        /// <summary>
        ///     RPC overrides for different chains. Uses Decentraland RPC endpoints.
        /// </summary>
        private static readonly Dictionary<BigInteger, string> RPC_OVERRIDES = new ()
        {
            { 1, "https://rpc.decentraland.org/mainnet" }, // Ethereum Mainnet
            { 11155111, "https://rpc.decentraland.org/sepolia" }, // Ethereum Sepolia
            { 137, "https://rpc.decentraland.org/polygon" }, // Polygon Mainnet
            { 80002, "https://rpc.decentraland.org/amoy" }, // Polygon Amoy
            { 42161, "https://rpc.decentraland.org/arbitrum" }, // Arbitrum Mainnet
            { 10, "https://rpc.decentraland.org/optimism" }, // Optimism Mainnet
            { 43114, "https://rpc.decentraland.org/avalanche" }, // Avalanche Mainnet
            { 56, "https://rpc.decentraland.org/binance" }, // BSC Mainnet
            { 250, "https://rpc.decentraland.org/fantom" }, // Fantom Mainnet
        };

        private readonly SemaphoreSlim mutex = new (1, 1);

        private readonly HashSet<string> whitelistMethods;
        private readonly HashSet<string> readOnlyMethods;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly int? identityExpirationDuration;
        private readonly BigInteger chainId;

        private readonly ThirdwebClient client;

        private TransactionConfirmationDelegate? transactionConfirmationCallback;

        private InAppWallet? pendingWallet;
        private UniTaskCompletionSource<bool>? loginCompletionSource;

        private IThirdwebWallet? activeWallet;

        internal ThirdWebAuthenticator(
            DecentralandEnvironment environment,
            HashSet<string> whitelistMethods,
            HashSet<string> readOnlyMethods,
            IWeb3AccountFactory web3AccountFactory,
            int? identityExpirationDuration = null)
        {
            this.whitelistMethods = whitelistMethods;
            this.readOnlyMethods = readOnlyMethods;
            this.web3AccountFactory = web3AccountFactory;
            this.identityExpirationDuration = identityExpirationDuration;

            chainId = ChainUtils.GetChainIdAsInt(environment);

            client = ThirdwebClient.Create(
                CLIENT_ID,
                bundleId: BUNDLE_ID,
                httpClient: new ThirdwebHttpClient(),
                sdkName: "UnitySDK",
                sdkOs: Application.platform.ToString(),
                sdkPlatform: "unity",
                sdkVersion: SDK_VERSION,
                rpcOverrides: RPC_OVERRIDES
            );
        }

        public void Dispose()
        {
            // Logout on Dispose will close ThirdWeb session and break ThirdWeb auto-login.
            // So we need to keep session open for auto-login to work.
        }

        public void SetTransactionConfirmationCallback(TransactionConfirmationDelegate callback)
        {
            transactionConfirmationCallback = callback;
        }

        public async UniTask<bool> TryAutoLoginAsync(CancellationToken ct)
        {
            string? email = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, null);

            if (string.IsNullOrEmpty(email))
                return false;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                InAppWallet? wallet = await InAppWallet.Create(
                    client,
                    email,
                    storageDirectoryPath: Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet"));

                if (!await wallet.IsConnected())
                    return false;

                activeWallet = wallet;
                ReportHub.Log(ReportCategory.AUTHENTICATION, "✅ ThirdWeb auto-login successful");
                return true;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.AUTHENTICATION, $"ThirdWeb auto-login failed with exception: {e.Message}");
                return false;
            }
        }

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(payload.Email))
                throw new ArgumentException("Email is required for OTP authentication", nameof(payload));

            await mutex.WaitAsync(ct);
            string email = payload.Email;

            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                activeWallet = await OTPLoginFlow(email, ct);

                string? sender = await activeWallet.GetAddress();

                IWeb3Account ephemeralAccount = web3AccountFactory.CreateRandomAccount();

                DateTime sessionExpiration = identityExpirationDuration != null
                    ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                    : DateTime.UtcNow.AddDays(7);

                var ephemeralMessage =
                    $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {sessionExpiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

                string signature = await activeWallet!.PersonalSign(ephemeralMessage);

                var authChain = AuthChain.Create();
                authChain.SetSigner(sender.ToLower());

                authChain.Set(new AuthLink
                {
                    type = signature.Length == 132
                        ? AuthLinkType.ECDSA_EPHEMERAL
                        : AuthLinkType.ECDSA_EIP_1654_EPHEMERAL,
                    payload = ephemeralMessage,
                    signature = signature,
                });

                return new DecentralandIdentity(new Web3Address(sender), ephemeralAccount, sessionExpiration, authChain);
            }
            catch (Exception)
            {
                await LogoutAsync(CancellationToken.None);
                throw;
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

        private async UniTask<InAppWallet> OTPLoginFlow(string? email, CancellationToken ct)
        {
            pendingWallet = await InAppWallet.Create(
                client,
                email,
                storageDirectoryPath: Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet"));

            await pendingWallet.SendOTP();
            ReportHub.Log(ReportCategory.AUTHENTICATION, "ThirdWeb login: OTP sent to email");

            // Wait for successful login via SubmitOtp
            loginCompletionSource = new UniTaskCompletionSource<bool>();
            ct.Register(() => loginCompletionSource?.TrySetCanceled(ct));

            await loginCompletionSource.Task;
            loginCompletionSource = null;
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb login: logged in as wallet {pendingWallet.WalletId}");

            // Store email for auto-login
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOGGEDIN_EMAIL, email);

            activeWallet = pendingWallet;
            InAppWallet result = pendingWallet;
            pendingWallet = null;
            return result;
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            if (activeWallet != null)
            {
                try { await activeWallet.Disconnect(); }
                finally { activeWallet = null; }
            }

            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL);
        }

        // Thirdweb's RPC endpoints
        private static string GetRpcUrl(int chainId) =>
            $"https://{chainId}.rpc.thirdweb.com";

        public async UniTask SubmitOtp(string otp)
        {
            if (pendingWallet == null)
                throw new InvalidOperationException("SubmitOtp called but no pending wallet");

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb login: validating OTP: {otp}");

            try
            {
                await pendingWallet.LoginWithOtp(otp);
                loginCompletionSource?.TrySetResult(true);
            }
            catch (InvalidOperationException e) when (e.Message.Contains("invalid or expired")) { throw new CodeVerificationException("Incorrect OTP code", e); }
        }

        public async UniTask ResendOtp()
        {
            if (pendingWallet == null)
                throw new InvalidOperationException("ResendOtp called but no pending wallet");

            await pendingWallet.SendOTP();
        }

        // Ethereum API
        public async UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct)
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
                    return await SendWithoutConfirmationAsync(request, ct);

                return await SendWithConfirmationAsync(request, source, ct);
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

        private async UniTask<EthApiResponse> SendWithoutConfirmationAsync(EthApiRequest request, CancellationToken ct)
        {
            ReportHub.LogError(ReportCategory.AUTHENTICATION, $"ThirdWeb web3 operation: Request method={request.method}, readonlyNetwork={request.readonlyNetwork ?? "null"}");

            // Determine target chainId: use readonlyNetwork if specified, otherwise use wallet's current chainId
            int? networkChainId = ChainUtils.GetChainIdFromReadonlyNetwork(request.readonlyNetwork);
            int targetChainId = networkChainId ?? (int)chainId;

            // eth_getBalance - can be handled locally for the active wallet (only if same chain)
            if (string.Equals(request.method, "eth_getBalance") && targetChainId == (int)chainId)
            {
                var address = request.@params[0].ToString();
                string walletAddress = await activeWallet!.GetAddress();

                if (string.Equals(address, walletAddress, StringComparison.OrdinalIgnoreCase))
                {
                    BigInteger balance = await activeWallet!.GetBalance(chainId);

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
        private async UniTask<EthApiResponse> SendWithConfirmationAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct)
        {
            // Request user confirmation before proceeding
            if (transactionConfirmationCallback != null)
            {
                TransactionConfirmationRequest confirmationRequest = await CreateConfirmationRequestAsync(request);

                // For Internal requests (Gifting, Donations, etc.), hide description and details panel
                // since they are already displayed in the feature-specific UI
                if (source == Web3RequestSource.Internal)
                {
                    confirmationRequest.HideDescription = true;
                    confirmationRequest.HideDetailsPanel = true;
                }

                bool confirmed = await transactionConfirmationCallback(confirmationRequest);

                if (!confirmed)
                    throw new Web3Exception("Transaction rejected by user");
            }

            // Wallet signing methods
            if (string.Equals(request.method, "personal_sign"))
            {
                // personal_sign params: [message, address]
                var message = request.@params[0].ToString();
                string signature = await activeWallet!.PersonalSign(message);

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
                string signature = await activeWallet!.SignTypedDataV4(typedDataJson);

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
                return await HandleSendTransactionAsync(request, useMetaTx);
            }

            // Fallback for any other non-read-only methods
            throw new Web3Exception($"Unsupported method requiring confirmation: {request.method}");
        }

        /// <summary>
        ///     Creates a confirmation request object with transaction details for the UI
        /// </summary>
        private async UniTask<TransactionConfirmationRequest> CreateConfirmationRequestAsync(EthApiRequest request)
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
                    BigInteger balanceWei = await activeWallet!.GetBalance(chainId);
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

                    string from = await activeWallet!.GetAddress();

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

        private async UniTask<EthApiResponse> HandleSendTransactionAsync(EthApiRequest request, bool useMetaTx = false)
        {
            Debug.Log($"[ThirdWeb] HandleSendTransactionAsync called, useMetaTx={useMetaTx}");

            if (activeWallet == null)
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
            BigInteger weiValue = ParseHexToBigInteger(value);
            Debug.Log($"[ThirdWeb] Parsed wei value: {weiValue}");

            // For meta-transactions (internal ops like gifting), use Decentraland relay
            // The user signs an EIP-712 message, and the relay pays for gas
            if (useMetaTx && !string.IsNullOrEmpty(data) && data != "0x")
            {
                Debug.Log($"[ThirdWeb] ★ Using meta-transaction for contract call to {to}");
                Debug.Log($"[ThirdWeb] Full data length: {data.Length} chars");
                string txHash = await SendMetaTransactionAsync(to, data);

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
                ThirdwebTransactionReceipt? txReceipt = await activeWallet!.Transfer(
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
            string hash = await ExecuteContractCallAsync(to, data, weiValue);

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
        private async UniTask<string> ExecuteContractCallAsync(string contractAddress, string data, BigInteger weiValue)
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
                activeWallet,
                contract,
                "execute",
                weiValue
            );

            // Override the data field with the actual pre-encoded calldata
            // This allows us to support ANY contract function, not just predefined ones
            transaction = transaction.SetData(data);

            return await ThirdwebTransaction.Send(transaction);
        }

#region Meta-Transactions (Decentraland Relay)
        // Mainnet relay server (Polygon chainId=137)
        private const string TRANSACTIONS_SERVER_URL_MAINNET = "https://transactions-api.decentraland.org/v1/transactions";

        // Testnet relay server (Amoy chainId=80002)
        private const string TRANSACTIONS_SERVER_URL_TESTNET = "https://transactions-api.decentraland.zone/v1/transactions";

        // Toggle for using manual EIP-712 hash calculation instead of ThirdWeb SignTypedDataV4
        private const bool USE_MANUAL_EIP712_SIGNING = true;

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
        private async UniTask<string> SendMetaTransactionAsync(string contractAddress, string functionSignature)
        {
            string from = await activeWallet!.GetAddress();

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
            string ourDomainSeparator = ComputeDomainSeparator(contractInfo.Name, contractInfo.Version, contractAddress, targetChainId);
            Debug.Log($"[ThirdWeb] Contract's domainSeparator: {contractDomainSeparator}");
            Debug.Log($"[ThirdWeb] Our computed domainSeparator: {ourDomainSeparator}");

            if (!string.Equals(contractDomainSeparator, ourDomainSeparator, StringComparison.OrdinalIgnoreCase))
                Debug.LogError("[ThirdWeb] ❌ DOMAIN SEPARATOR MISMATCH! Contract uses different EIP-712 parameters.");

            string signature;

            if (USE_MANUAL_EIP712_SIGNING)
            {
                // Manual EIP-712: compute hash with Nethereum and sign via eth_sign (raw hash signing)
                signature = await SignMetaTxManuallyAsync(
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
                string typedDataJson = CreateMetaTxTypedData(
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
                signature = await activeWallet!.SignTypedDataV4(typedDataJson);
            }

            Debug.Log($"[ThirdWeb] Full signature: {signature}");
            Debug.Log($"[ThirdWeb] Signature length: {signature.Length}");

            // 5. Encode executeMetaTransaction call data
            string txData = EncodeExecuteMetaTransaction(from, signature, functionSignature);
            Debug.Log($"[ThirdWeb] Encoded executeMetaTransaction: {txData[..50]}...");

            // 6. POST to transactions-server (use appropriate server based on chainId)
            return await PostToTransactionsServerAsync(from, contractAddress, txData, targetChainId);
        }

        // Toggle to use raw hash signing instead of SignTypedDataV4
        // This bypasses ThirdWeb's EIP-712 implementation and signs the hash directly
        private const bool USE_RAW_HASH_SIGNING = false;

        /// <summary>
        ///     Manually computes EIP-712 hash using Nethereum and verifies/debugs the signing process.
        ///     Can either sign via ThirdWeb SignTypedDataV4 or sign the raw hash directly.
        /// </summary>
        private async UniTask<string> SignMetaTxManuallyAsync(
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
            string typedDataJson = CreateMetaTxTypedData(
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
                manualHash = ComputeEip712HashManually(contractName, contractVersion, contractAddress, chainIdValue, nonce, from, functionSignature);
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
                manualHash = ComputeEip712HashManually(contractName, contractVersion, contractAddress, chainIdValue, nonce, from, functionSignature);
                nethereumHash = manualHash;
                Debug.Log($"[EIP712-Manual] Using manual computed HASH: {manualHash}");
            }

            string signature;

            if (USE_RAW_HASH_SIGNING && !string.IsNullOrEmpty(manualHash))
            {
                // Alternative approach: Sign the raw hash directly
                // This bypasses ThirdWeb's SignTypedDataV4 implementation
                Debug.Log("[EIP712-Manual] Using RAW HASH SIGNING approach...");
                signature = await SignRawHashAsync(from, manualHash);
            }
            else
            {
                // Standard approach: Sign via ThirdWeb SignTypedDataV4
                Debug.Log("[EIP712-Manual] Signing via ThirdWeb SignTypedDataV4...");
                signature = await activeWallet!.SignTypedDataV4(typedDataJson);
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
        private async UniTask<string> SignRawHashAsync(string signerAddress, string hashHex)
        {
            Debug.Log($"[EIP712-Manual] Signing raw hash: {hashHex}");

            // Convert hash to bytes
            byte[] hashBytes = HexToBytes(hashHex);

            if (hashBytes.Length != 32)
            {
                Debug.LogError($"[EIP712-Manual] Hash must be 32 bytes, got {hashBytes.Length}");
                throw new Exception("Invalid hash length for signing");
            }

            // Try using PersonalSign with raw bytes
            // WARNING: PersonalSign adds "\x19Ethereum Signed Message:\n32" prefix!
            // This will NOT work directly with contract's ecrecover which expects EIP-712 format
            string signature = await activeWallet!.PersonalSign(hashBytes);

            Debug.Log($"[EIP712-Manual] Raw hash signature (with personal_sign prefix): {signature}");
            Debug.LogWarning("[EIP712-Manual] NOTE: PersonalSign adds message prefix - contract may not accept this!");

            return signature;
        }

        /// <summary>
        ///     Computes EIP-712 hash manually following the exact algorithm.
        ///     Addresses are used as-is (like JS library).
        /// </summary>
        private static string ComputeEip712HashManually(
            string contractName,
            string contractVersion,
            string contractAddress,
            int chainIdValue,
            BigInteger nonce,
            string from,
            string functionSignature)
        {
            var sha3 = new Sha3Keccack();

            // Use addresses as-is (hex to bytes conversion is case-insensitive)

            // ========== TYPE HASHES ==========
            // EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)
            const string DOMAIN_TYPE = "EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)";
            byte[] domainTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(DOMAIN_TYPE));
            Debug.Log($"[EIP712-Hash] Domain type hash: 0x{BytesToHex(domainTypeHash)}");

            // MetaTransaction(uint256 nonce,address from,bytes functionSignature)
            const string META_TX_TYPE = "MetaTransaction(uint256 nonce,address from,bytes functionSignature)";
            byte[] metaTxTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(META_TX_TYPE));
            Debug.Log($"[EIP712-Hash] MetaTx type hash: 0x{BytesToHex(metaTxTypeHash)}");

            // ========== DOMAIN SEPARATOR ==========
            byte[] nameHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractName));
            byte[] versionHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractVersion));
            Debug.Log($"[EIP712-Hash] Name hash ('{contractName}'): 0x{BytesToHex(nameHash)}");
            Debug.Log($"[EIP712-Hash] Version hash ('{contractVersion}'): 0x{BytesToHex(versionHash)}");

            // Salt is chainId padded to bytes32
            var salt = new byte[32];
            byte[] chainIdBytes = BitConverter.GetBytes(chainIdValue);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(chainIdBytes);

            Array.Copy(chainIdBytes, 0, salt, 32 - chainIdBytes.Length, chainIdBytes.Length);
            Debug.Log($"[EIP712-Hash] Salt (chainId={chainIdValue}): 0x{BytesToHex(salt)}");

            // verifyingContract as bytes32 (address padded to 32 bytes)
            byte[] contractAddressBytes = HexToBytes(contractAddress);
            var contractAddressPadded = new byte[32];
            Array.Copy(contractAddressBytes, 0, contractAddressPadded, 32 - contractAddressBytes.Length, contractAddressBytes.Length);
            Debug.Log($"[EIP712-Hash] Contract address padded: 0x{BytesToHex(contractAddressPadded)}");

            // Encode domain separator: abi.encode(typeHash, nameHash, versionHash, contractAddress, salt)
            byte[] domainEncoded = ConcatBytes(domainTypeHash, nameHash, versionHash, contractAddressPadded, salt);
            byte[] domainSeparator = sha3.CalculateHash(domainEncoded);
            Debug.Log($"[EIP712-Hash] ★ Our computed DOMAIN SEPARATOR: 0x{BytesToHex(domainSeparator)}");

            // ========== STRUCT HASH ==========
            // nonce as uint256 (32 bytes)
            var nonceBytes = new byte[32];
            byte[] nonceBigEndian = nonce.ToByteArray();

            if (BitConverter.IsLittleEndian && nonceBigEndian.Length > 1)
                Array.Reverse(nonceBigEndian);

            if (nonceBigEndian.Length > 0 && nonceBigEndian[0] == 0 && nonceBigEndian.Length > 1)
                nonceBigEndian = nonceBigEndian[1..];

            Array.Copy(nonceBigEndian, 0, nonceBytes, 32 - nonceBigEndian.Length, nonceBigEndian.Length);
            Debug.Log($"[EIP712-Hash] Nonce ({nonce}): 0x{BytesToHex(nonceBytes)}");

            // from address as bytes32
            byte[] fromBytes = HexToBytes(from);
            var fromPadded = new byte[32];
            Array.Copy(fromBytes, 0, fromPadded, 32 - fromBytes.Length, fromBytes.Length);
            Debug.Log($"[EIP712-Hash] From address padded: 0x{BytesToHex(fromPadded)}");

            // keccak256(functionSignature)
            byte[] funcSigBytes = HexToBytes(functionSignature);
            byte[] funcSigHash = sha3.CalculateHash(funcSigBytes);
            Debug.Log($"[EIP712-Hash] Function sig hash: 0x{BytesToHex(funcSigHash)}");

            byte[] structEncoded = ConcatBytes(metaTxTypeHash, nonceBytes, fromPadded, funcSigHash);
            byte[] structHash = sha3.CalculateHash(structEncoded);
            Debug.Log($"[EIP712-Hash] Struct hash: 0x{BytesToHex(structHash)}");

            // ========== FINAL DIGEST ==========
            var prefix = new byte[] { 0x19, 0x01 };
            byte[] digest = sha3.CalculateHash(ConcatBytes(prefix, domainSeparator, structHash));
            Debug.Log($"[EIP712-Hash] ★ Final digest: 0x{BytesToHex(digest)}");

            return "0x" + BytesToHex(digest);
        }

        /// <summary>
        ///     Computes domain separator for comparison with contract.
        ///     Uses DCL/Matic format: EIP712Domain(name,version,verifyingContract,salt)
        /// </summary>
        public static string ComputeDomainSeparator(string contractName, string contractVersion, string contractAddress, int chainId)
        {
            var sha3 = new Sha3Keccack();

            // DCL/Matic format with salt
            const string DOMAIN_TYPE = "EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)";
            byte[] domainTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(DOMAIN_TYPE));
            byte[] nameHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractName));
            byte[] versionHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractVersion));

            var salt = new byte[32];
            byte[] chainIdBytes = BitConverter.GetBytes(chainId);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(chainIdBytes);

            Array.Copy(chainIdBytes, 0, salt, 32 - chainIdBytes.Length, chainIdBytes.Length);

            byte[] contractAddressBytes = HexToBytes(contractAddress);
            var contractAddressPadded = new byte[32];
            Array.Copy(contractAddressBytes, 0, contractAddressPadded, 32 - contractAddressBytes.Length, contractAddressBytes.Length);

            byte[] domainEncoded = ConcatBytes(domainTypeHash, nameHash, versionHash, contractAddressPadded, salt);
            byte[] domainSeparator = sha3.CalculateHash(domainEncoded);

            return "0x" + BytesToHex(domainSeparator);
        }

        /// <summary>
        ///     Computes domain separator using STANDARD EIP-712 format with chainId (not salt).
        ///     Format: EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)
        /// </summary>
        public static string ComputeDomainSeparatorStandard(string contractName, string contractVersion, string contractAddress, int chainId)
        {
            var sha3 = new Sha3Keccack();

            // Standard EIP-712 format with chainId (uint256)
            const string DOMAIN_TYPE = "EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)";
            byte[] domainTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(DOMAIN_TYPE));
            Debug.Log($"[DomainSep-Std] Type hash: 0x{BytesToHex(domainTypeHash)}");

            byte[] nameHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractName));
            byte[] versionHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractVersion));
            Debug.Log($"[DomainSep-Std] Name hash ('{contractName}'): 0x{BytesToHex(nameHash)}");
            Debug.Log($"[DomainSep-Std] Version hash ('{contractVersion}'): 0x{BytesToHex(versionHash)}");

            // chainId as uint256 (32 bytes, big-endian)
            var chainIdPadded = new byte[32];
            byte[] chainIdBytes = BitConverter.GetBytes(chainId);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(chainIdBytes);

            Array.Copy(chainIdBytes, 0, chainIdPadded, 32 - chainIdBytes.Length, chainIdBytes.Length);
            Debug.Log($"[DomainSep-Std] ChainId ({chainId}): 0x{BytesToHex(chainIdPadded)}");

            // verifyingContract as address (20 bytes padded to 32)
            byte[] contractAddressBytes = HexToBytes(contractAddress);
            var contractAddressPadded = new byte[32];
            Array.Copy(contractAddressBytes, 0, contractAddressPadded, 32 - contractAddressBytes.Length, contractAddressBytes.Length);
            Debug.Log($"[DomainSep-Std] Contract: 0x{BytesToHex(contractAddressPadded)}");

            // Order: typeHash, nameHash, versionHash, chainId, verifyingContract
            byte[] domainEncoded = ConcatBytes(domainTypeHash, nameHash, versionHash, chainIdPadded, contractAddressPadded);
            byte[] domainSeparator = sha3.CalculateHash(domainEncoded);

            return "0x" + BytesToHex(domainSeparator);
        }

        /// <summary>
        ///     Computes domain separator with MINIMAL fields (name, version, chainId only).
        ///     Some contracts use this simpler format.
        /// </summary>
        public static string ComputeDomainSeparatorMinimal(string contractName, string contractVersion, int chainId)
        {
            var sha3 = new Sha3Keccack();

            const string DOMAIN_TYPE = "EIP712Domain(string name,string version,uint256 chainId)";
            byte[] domainTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(DOMAIN_TYPE));
            byte[] nameHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractName));
            byte[] versionHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractVersion));

            var chainIdPadded = new byte[32];
            byte[] chainIdBytes = BitConverter.GetBytes(chainId);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(chainIdBytes);

            Array.Copy(chainIdBytes, 0, chainIdPadded, 32 - chainIdBytes.Length, chainIdBytes.Length);

            byte[] domainEncoded = ConcatBytes(domainTypeHash, nameHash, versionHash, chainIdPadded);
            byte[] domainSeparator = sha3.CalculateHash(domainEncoded);

            return "0x" + BytesToHex(domainSeparator);
        }

        private static byte[] HexToBytes(string hex)
        {
            string clean = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            if (clean.Length % 2 != 0)
                clean = "0" + clean;

            var bytes = new byte[clean.Length / 2];

            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);

            return bytes;
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
                sb.AppendFormat("{0:x2}", b);

            return sb.ToString();
        }

        private static byte[] ConcatBytes(params byte[][] arrays)
        {
            var totalLength = 0;

            foreach (byte[] arr in arrays)
                totalLength += arr.Length;

            var result = new byte[totalLength];
            var offset = 0;

            foreach (byte[] arr in arrays)
            {
                Array.Copy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }

            return result;
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
            return ParseHexToBigInteger(response.result?.ToString() ?? "0x0");
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

            return DecodeStringFromHex(hex);
        }

        /// <summary>
        ///     Creates EIP-712 typed data JSON for meta-transaction signing.
        ///     NOTE: JS library (decentraland-transactions) does NOT lowercase addresses.
        ///     It uses account/contractAddress as-is from the wallet (usually checksum format).
        ///     EIP-712 'address' type is encoded as bytes, so case shouldn't matter for the hash.
        ///
        ///     IMPORTANT: We use explicit JSON construction to ensure exact key ordering
        ///     matches the JS library. JsonConvert with anonymous types may reorder keys.
        /// </summary>
        private static string CreateMetaTxTypedData(
            string contractName,
            string contractVersion,
            string contractAddress,
            int chainIdValue,
            BigInteger nonce,
            string from,
            string functionSignature)
        {
            // Salt is chainId padded to bytes32
            string salt = "0x" + chainIdValue.ToString("x").PadLeft(64, '0');

            // Build JSON manually to ensure exact key order matches JS library
            // JS object key order: types, domain, primaryType, message
            // (Note: EIP-712 shouldn't care about JSON key order, but SDK implementations might)
            var sb = new StringBuilder();
            sb.Append('{');

            // types
            sb.Append("\"types\":{");
            sb.Append("\"EIP712Domain\":[");
            sb.Append("{\"name\":\"name\",\"type\":\"string\"},");
            sb.Append("{\"name\":\"version\",\"type\":\"string\"},");
            sb.Append("{\"name\":\"verifyingContract\",\"type\":\"address\"},");
            sb.Append("{\"name\":\"salt\",\"type\":\"bytes32\"}");
            sb.Append("],");
            sb.Append("\"MetaTransaction\":[");
            sb.Append("{\"name\":\"nonce\",\"type\":\"uint256\"},");
            sb.Append("{\"name\":\"from\",\"type\":\"address\"},");
            sb.Append("{\"name\":\"functionSignature\",\"type\":\"bytes\"}");
            sb.Append("]},");

            // domain - use JsonConvert for proper escaping of contract name
            sb.Append("\"domain\":{");
            sb.Append($"\"name\":{JsonConvert.SerializeObject(contractName)},");
            sb.Append($"\"version\":{JsonConvert.SerializeObject(contractVersion)},");
            sb.Append($"\"verifyingContract\":\"{contractAddress}\",");
            sb.Append($"\"salt\":\"{salt}\"");
            sb.Append("},");

            // primaryType
            sb.Append("\"primaryType\":\"MetaTransaction\",");

            // message
            sb.Append("\"message\":{");
            sb.Append($"\"nonce\":{(long)nonce},"); // Must be a number, not string!
            sb.Append($"\"from\":\"{from}\",");
            sb.Append($"\"functionSignature\":\"{functionSignature}\"");
            sb.Append('}');

            sb.Append('}');

            var result = sb.ToString();
            Debug.Log($"[ThirdWeb] Created typed data JSON (length={result.Length}):\n{result}");
            return result;
        }

        /// <summary>
        ///     Encodes the executeMetaTransaction(address,bytes,bytes32,bytes32,uint8) call.
        ///     This is what gets sent to the transactions-server as the second param.
        ///     Based on: https://github.com/decentraland/decentraland-transactions/blob/master/src/utils.ts
        /// </summary>
        private static string EncodeExecuteMetaTransaction(string userAddress, string signature, string functionSignature)
        {
            // executeMetaTransaction selector = 0x0c53c51c
            const string EXECUTE_META_TX_SELECTOR = "0c53c51c";

            string sig = signature.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? signature[2..]
                : signature;

            string r = sig[..64];
            string s = sig.Substring(64, 64);
            var vInt = Convert.ToInt32(sig.Substring(128, 2), 16);

            // Normalize v value (some wallets return 0/1 instead of 27/28)
            if (vInt < 27)
                vInt += 27;

            string v = vInt.ToString("x").PadLeft(64, '0');

            Debug.Log($"[ThirdWeb] Signature components: r={r}, s={s}, v={vInt} (0x{v})");

            string method = functionSignature.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? functionSignature[2..]
                : functionSignature;

            string signatureLength = (method.Length / 2).ToString("x").PadLeft(64, '0');
            var signaturePadding = (int)Math.Ceiling(method.Length / 64.0);

            // JS library does NOT toLowerCase here - just strips 0x and pads
            string address = userAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? userAddress[2..]
                : userAddress;

            Debug.Log($"[ThirdWeb] Encoding executeMetaTransaction: address={address}, methodLen={method.Length / 2}");

            // Build the encoded call:
            // selector + address(32) + offset(32) + r(32) + s(32) + v(32) + length(32) + data(padded)
            return string.Concat(
                "0x",
                EXECUTE_META_TX_SELECTOR,
                address.PadLeft(64, '0'), // userAddress - NO toLowerCase, just like JS!
                "a0".PadLeft(64, '0'), // offset to functionSignature (160 = 0xa0)
                r, // r
                s, // s
                v, // v
                signatureLength, // length of functionSignature
                method.PadRight(64 * signaturePadding, '0') // functionSignature padded
            );
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
                System.Text.Encoding.UTF8,
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
        ///     Decodes a hex-encoded string from eth_call result.
        /// </summary>
        private static string DecodeStringFromHex(string hex)
        {
            Debug.Log($"[ThirdWeb] DecodeStringFromHex input length: {hex?.Length ?? 0}");

            if (string.IsNullOrEmpty(hex) || hex.Length < 130)
            {
                Debug.LogWarning($"[ThirdWeb] Hex too short to decode: {hex}");
                return string.Empty;
            }

            string clean = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            // Skip offset (32 bytes) and length (32 bytes), then read string data
            // Format: offset (32) + length (32) + data (variable)
            var lengthOffset = 64; // Skip offset
            var length = Convert.ToInt32(clean.Substring(lengthOffset, 64), 16);
            Debug.Log($"[ThirdWeb] Decoded string length: {length}");

            if (length == 0)
                return string.Empty;

            int dataOffset = lengthOffset + 64;
            int hexLength = Math.Min(length * 2, clean.Length - dataOffset);

            if (hexLength <= 0)
                return string.Empty;

            string dataHex = clean.Substring(dataOffset, hexLength);
            var bytes = new byte[dataHex.Length / 2];

            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(dataHex.Substring(i * 2, 2), 16);

            string result = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            Debug.Log($"[ThirdWeb] Decoded string result: '{result}'");
            return result;
        }

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
#endregion

        private static BigInteger ParseHexToBigInteger(string hexValue)
        {
            if (string.IsNullOrEmpty(hexValue) || hexValue == "0x" || hexValue == "0x0")
                return BigInteger.Zero;

            string hex = hexValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? hexValue[2..]
                : hexValue;

            return BigInteger.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        }


    }
}
