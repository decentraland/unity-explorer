using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Prefs;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using Thirdweb;
using ThirdWebUnity;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public class ThirdWebAuthenticator : IWeb3VerifiedAuthenticator, IEthereumApi
    {
        public static ThirdWebAuthenticator Instance;

        private readonly SemaphoreSlim mutex = new (1, 1);

        private readonly HashSet<string> whitelistMethods;
        private readonly HashSet<string> readOnlyMethods;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly int? identityExpirationDuration;

        private TransactionConfirmationDelegate? transactionConfirmationCallback;

        private BigInteger chainId;
        private InAppWallet? pendingWallet;
        private UniTaskCompletionSource<bool>? loginCompletionSource;

        // Minimal ABI with a dummy function to create a base transaction that we'll customize
        // This allows us to support ANY contract call by overriding the data field
        private const string MINIMAL_ABI = @"[{""name"":""execute"",""type"":""function"",""inputs"":[],""outputs"":[]}]";

        public ThirdWebAuthenticator(DecentralandEnvironment environment, HashSet<string> whitelistMethods,
            HashSet<string> readOnlyMethods, IWeb3AccountFactory web3AccountFactory, int? identityExpirationDuration = null)
        {
            Instance?.Dispose();
            Instance = this;

            this.whitelistMethods = whitelistMethods;
            this.readOnlyMethods = readOnlyMethods;
            this.web3AccountFactory = web3AccountFactory;
            this.identityExpirationDuration = identityExpirationDuration;

            chainId = EnvChainsUtils.GetChainIdAsInt(environment);
        }

        public void SetTransactionConfirmationCallback(TransactionConfirmationDelegate callback)
        {
            transactionConfirmationCallback = callback;
        }

        public async UniTask<bool> TryAutoConnectAsync(CancellationToken ct)
        {
            string? email = DCLPlayerPrefs.GetString(DCLPrefKeys.LOGGEDIN_EMAIL, null);
            Debug.Log("[ThirdWeb] Session expired, auto-connect failed");

            if (string.IsNullOrEmpty(email))
                return false;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                InAppWallet? wallet = await InAppWallet.Create(
                    ThirdWebManager.Instance.Client,
                    email,
                    storageDirectoryPath: Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet"));

                if (!await wallet.IsConnected())
                {
                    Debug.Log("[ThirdWeb] Session expired, auto-connect failed");
                    return false;
                }

                ThirdWebManager.Instance.ActiveWallet = wallet;
                Debug.Log("[ThirdWeb] Auto-connect successful");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ThirdWeb] Auto-connect failed: {e.Message}");
                return false;
            }
        }

        public UniTask<IWeb3Identity> LoginAsync(LoginMethod loginMethod, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<IWeb3Identity> LoginPayloadedAsync<TPayload>(LoginMethod method, TPayload payload, CancellationToken ct)
        {
            await mutex.WaitAsync(ct);
            var email = payload as string;

            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                ThirdWebManager.Instance.ActiveWallet
                    = await LoginViaOTP(email, ct);

                string? sender = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

                IWeb3Account ephemeralAccount = web3AccountFactory.CreateRandomAccount();

                // 1 week expiration day, just like unity-renderer
                DateTime sessionExpiration = identityExpirationDuration != null
                    ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                    : DateTime.UtcNow.AddDays(7);

                var ephemeralMessage =
                    $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {sessionExpiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

                string signature = await ThirdWebManager.Instance.ActiveWallet.PersonalSign(ephemeralMessage);

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

        private async UniTask<InAppWallet> LoginViaOTP(string? email, CancellationToken ct)
        {
            Debug.Log("Login via OTP");

            pendingWallet = await InAppWallet.Create(
                ThirdWebManager.Instance.Client,
                email,
                storageDirectoryPath: Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet"));

            await pendingWallet.SendOTP();

            Debug.Log("OTP sent to email");

            // Wait for successful login via SubmitOtp
            loginCompletionSource = new UniTaskCompletionSource<bool>();
            ct.Register(() => loginCompletionSource?.TrySetCanceled(ct));

            await loginCompletionSource.Task;
            loginCompletionSource = null;

            Debug.Log($"ThirdWeb logged in as wallet {pendingWallet.WalletId}");

            // Store email for auto-login
            DCLPlayerPrefs.SetString(DCLPrefKeys.LOGGEDIN_EMAIL, email);

            ThirdWebManager.Instance.ActiveWallet = pendingWallet;
            InAppWallet result = pendingWallet;
            pendingWallet = null;
            return result;
        }

        public void Dispose()
        {
            // Logout on Dispose will close session and break ThirdWeb auto-login. So we need to keep session open for auto-login.
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            await ThirdWebManager.Instance?.DisconnectWallet();
        }

        public void SetSepoliaChain()
        {
            chainId = new BigInteger(11155111);
        }

        public async UniTask<EthApiResponse> SendAsync(int chainId, EthApiRequest request, CancellationToken ct)
        {
            var targetChainId = new BigInteger(chainId);

            this.chainId = targetChainId;
            await ThirdWebManager.Instance.ActiveWallet.SwitchNetwork(this.chainId);

            return await SendAsync(request, ct);
        }

        public async UniTask<EthApiResponse> SendAsync(int chainId, EthApiRequest request, Web3RequestSource source, CancellationToken ct)
        {
            var targetChainId = new BigInteger(chainId);

            this.chainId = targetChainId;
            await ThirdWebManager.Instance.ActiveWallet.SwitchNetwork(this.chainId);

            return await SendAsync(request, source, ct);
        }

        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct) =>
            SendAsync(request, Web3RequestSource.SDKScene, ct);

        public async UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct)
        {
            await mutex.WaitAsync(ct);
            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                if (!whitelistMethods.Contains(request.method))
                    throw new Web3Exception($"The method is not allowed: {request.method}");

                if (IsReadOnly(request))
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

        /// <summary>
        ///     Handles read-only RPC methods that don't require user confirmation
        /// </summary>
        private async UniTask<EthApiResponse> SendWithoutConfirmationAsync(EthApiRequest request, CancellationToken ct)
        {
            // eth_getBalance - can be handled locally for the active wallet
            if (string.Equals(request.method, "eth_getBalance"))
            {
                var address = request.@params[0].ToString();
                string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

                if (string.Equals(address, walletAddress, StringComparison.OrdinalIgnoreCase))
                {
                    BigInteger balance = await ThirdWebManager.Instance.ActiveWallet.GetBalance(chainId);

                    return new EthApiResponse
                    {
                        id = request.id,
                        jsonrpc = "2.0",
                        result = "0x" + balance.ToString("x"),
                    };
                }
            }

            // All other read-only RPC methods - delegate to low-level RPC calls
            return await SendRpcRequestAsync(request);
        }

        /// <summary>
        ///     Handles methods that require user confirmation (signing, transactions)
        /// </summary>
        private async UniTask<EthApiResponse> SendWithConfirmationAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct)
        {
            // Request user confirmation before proceeding, but only for SDKScene requests.
            // Internal requests (Gifting, Donations, etc.) skip this UI since they have their own confirmation flow.
            if (source == Web3RequestSource.SDKScene && transactionConfirmationCallback != null)
            {
                TransactionConfirmationRequest confirmationRequest = await CreateConfirmationRequestAsync(request);
                bool confirmed = await transactionConfirmationCallback(confirmationRequest);

                if (!confirmed)
                    throw new Web3Exception("Transaction rejected by user");
            }

            // Wallet signing methods
            if (string.Equals(request.method, "personal_sign"))
            {
                // personal_sign params: [message, address]
                var message = request.@params[0].ToString();
                string signature = await ThirdWebManager.Instance.ActiveWallet.PersonalSign(message);

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
                string signature = await ThirdWebManager.Instance.ActiveWallet.SignTypedDataV4(typedDataJson);

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
                NetworkName = GetNetworkName((int)chainId),
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
                    BigInteger balanceWei = await ThirdWebManager.Instance.ActiveWallet.GetBalance(chainId);
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

                    string from = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

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

        private static string GetNetworkName(int chainId) =>
            chainId switch
            {
                1 => "Ethereum Mainnet",
                11155111 => "Sepolia",
                _ => $"Chain {chainId}",
            };

        private async UniTask<EthApiResponse> HandleSendTransactionAsync(EthApiRequest request, bool useMetaTx = false)
        {
            if (ThirdWebManager.Instance.ActiveWallet == null)
                throw new Web3Exception("No active wallet connected");

            // eth_sendTransaction params: [transactionObject]
            Dictionary<string, object>? txParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.@params[0].ToString());

            string? to = txParams?.TryGetValue("to", out object? toValue) == true ? toValue?.ToString() : null;
            string data = txParams?.TryGetValue("data", out object? dataValue) == true ? dataValue?.ToString() ?? "0x" : "0x";
            string value = txParams?.TryGetValue("value", out object? valueValue) == true ? valueValue?.ToString() ?? "0x0" : "0x0";

            if (string.IsNullOrEmpty(to))
                throw new Web3Exception("eth_sendTransaction requires 'to' address");

            // Parse value
            BigInteger weiValue = ParseHexToBigInteger(value);

            // For meta-transactions (internal ops like gifting), use Decentraland relay
            // The user signs an EIP-712 message, and the relay pays for gas
            if (useMetaTx && !string.IsNullOrEmpty(data) && data != "0x")
            {
                Debug.Log($"[ThirdWeb] Using meta-transaction for contract call to {to}");
                string txHash = await SendMetaTransactionAsync(to, data);

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
                ThirdwebTransactionReceipt? txReceipt = await ThirdWebManager.Instance.ActiveWallet.Transfer(
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
                ThirdWebManager.Instance.Client,
                contractAddress,
                chainId,
                MINIMAL_ABI
            );

            // Create a base transaction using the dummy function
            ThirdwebTransaction transaction = await ThirdwebContract.Prepare(
                ThirdWebManager.Instance.ActiveWallet,
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
        private const string TRANSACTIONS_SERVER_URL = "https://transactions-api.decentraland.org/v1/transactions";

        /// <summary>
        ///     Sends a meta-transaction via Decentraland's transactions-server.
        ///     The user signs an EIP-712 message, and the server relays the transaction paying for gas.
        /// </summary>
        private async UniTask<string> SendMetaTransactionAsync(string contractAddress, string functionSignature)
        {
            string from = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

            Debug.Log("[ThirdWeb] Sending meta-transaction via Decentraland relay");
            Debug.Log($"[ThirdWeb] From: {from}, Contract: {contractAddress}");
            Debug.Log($"[ThirdWeb] Function signature: {functionSignature}");

            // 1. Get meta-tx nonce from the contract
            BigInteger nonce = await GetMetaTxNonceAsync(contractAddress, from);
            Debug.Log($"[ThirdWeb] Meta-tx nonce: {nonce}");

            // 2. Get contract data for EIP-712 domain
            ContractMetaTxInfo contractInfo = await GetContractMetaTxInfoAsync(contractAddress);
            Debug.Log($"[ThirdWeb] Contract info - Name: {contractInfo.Name}, Version: {contractInfo.Version}");

            // 3. Create EIP-712 typed data
            string typedDataJson = CreateMetaTxTypedData(
                contractInfo.Name,
                contractInfo.Version,
                contractAddress,
                (int)chainId,
                nonce,
                from,
                functionSignature
            );

            Debug.Log("[ThirdWeb] EIP-712 typed data created");

            // 4. Sign with ThirdWeb wallet
            string signature = await ThirdWebManager.Instance.ActiveWallet.SignTypedDataV4(typedDataJson);
            Debug.Log($"[ThirdWeb] Signature obtained: {signature[..20]}...");

            // 5. Split signature into r, s, v components
            (string r, string s, int v) = SplitSignature(signature);

            // 6. POST to transactions-server
            return await PostToTransactionsServerAsync(from, contractAddress, functionSignature, r, s, v);
        }

        /// <summary>
        ///     Gets the meta-transaction nonce for a user from the contract.
        ///     Calls getNonce(address) on the contract.
        /// </summary>
        private async UniTask<BigInteger> GetMetaTxNonceAsync(string contractAddress, string userAddress)
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

            EthApiResponse response = await SendRpcRequestAsync(request);
            return ParseHexToBigInteger(response.result?.ToString() ?? "0x0");
        }

        /// <summary>
        ///     Gets contract metadata needed for EIP-712 domain.
        ///     First tries known contracts, then falls back to on-chain queries.
        /// </summary>
        private async UniTask<ContractMetaTxInfo> GetContractMetaTxInfoAsync(string contractAddress)
        {
            string addressLower = contractAddress.ToLower();

            // Check known Decentraland contracts first
            if (KnownMetaTxContracts.TryGetValue(addressLower, out ContractMetaTxInfo? known))
                return known;

            // For collection contracts (like the one in gifting), try to get name from contract
            // Most DCL collections use "Decentraland Collection" as name and "2" as version
            try
            {
                string name = await GetContractNameAsync(contractAddress);

                if (!string.IsNullOrEmpty(name))
                    return new ContractMetaTxInfo(name, "2");
            }
            catch (Exception e) { Debug.LogWarning($"[ThirdWeb] Failed to get contract name: {e.Message}"); }

            // Fallback for DCL collection contracts
            return new ContractMetaTxInfo("Decentraland Collection", "2");
        }

        /// <summary>
        ///     Calls name() on the contract to get its EIP-712 domain name.
        /// </summary>
        private async UniTask<string> GetContractNameAsync(string contractAddress)
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

            EthApiResponse response = await SendRpcRequestAsync(request);
            var hex = response.result?.ToString();

            if (string.IsNullOrEmpty(hex) || hex == "0x")
                return string.Empty;

            return DecodeStringFromHex(hex);
        }

        /// <summary>
        ///     Creates EIP-712 typed data JSON for meta-transaction signing.
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

            var typedData = new
            {
                types = new
                {
                    EIP712Domain = new object[]
                    {
                        new { name = "name", type = "string" },
                        new { name = "version", type = "string" },
                        new { name = "verifyingContract", type = "address" },
                        new { name = "salt", type = "bytes32" },
                    },
                    MetaTransaction = new object[]
                    {
                        new { name = "nonce", type = "uint256" },
                        new { name = "from", type = "address" },
                        new { name = "functionSignature", type = "bytes" },
                    },
                },
                primaryType = "MetaTransaction",
                domain = new
                {
                    name = contractName,
                    version = contractVersion,
                    verifyingContract = contractAddress,
                    salt,
                },
                message = new
                {
                    nonce = nonce.ToString(),
                    from,
                    functionSignature,
                },
            };

            return JsonConvert.SerializeObject(typedData);
        }

        /// <summary>
        ///     Splits an Ethereum signature into r, s, v components.
        /// </summary>
        private static (string r, string s, int v) SplitSignature(string signature)
        {
            string sig = signature.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? signature[2..]
                : signature;

            string r = "0x" + sig[..64];
            string s = "0x" + sig.Substring(64, 64);
            var v = Convert.ToInt32(sig.Substring(128, 2), 16);

            // Normalize v value (some wallets return 0/1 instead of 27/28)
            if (v < 27)
                v += 27;

            return (r, s, v);
        }

        /// <summary>
        ///     POSTs the signed meta-transaction to Decentraland's transactions-server.
        /// </summary>
        private async UniTask<string> PostToTransactionsServerAsync(
            string from,
            string contractAddress,
            string functionSignature,
            string sigR,
            string sigS,
            int sigV)
        {
            var payload = new
            {
                transactionData = new
                {
                    from,
                    @params = new[] { contractAddress, functionSignature },
                    userAddress = from,
                    contractAddress,
                    functionSignature,
                    sigR,
                    sigS,
                    sigV,
                },
            };

            string payloadJson = JsonConvert.SerializeObject(payload);
            Debug.Log($"[ThirdWeb] Posting to transactions-server: {TRANSACTIONS_SERVER_URL}");

            IThirdwebHttpClient httpClient = ThirdWebManager.Instance.Client.HttpClient;

            var content = new System.Net.Http.StringContent(
                payloadJson,
                System.Text.Encoding.UTF8,
                "application/json"
            );

            ThirdwebHttpResponseMessage response = await httpClient.PostAsync(
                TRANSACTIONS_SERVER_URL,
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
            if (string.IsNullOrEmpty(hex) || hex.Length < 130)
                return string.Empty;

            string clean = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            // Skip offset (32 bytes) and length (32 bytes), then read string data
            // Format: offset (32) + length (32) + data (variable)
            var lengthOffset = 64; // Skip offset
            var length = Convert.ToInt32(clean.Substring(lengthOffset, 64), 16);

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

            return System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }

        /// <summary>
        ///     Known Decentraland contracts that support meta-transactions.
        ///     Key = lowercase contract address, Value = (name, version) for EIP-712 domain.
        /// </summary>
        private static readonly Dictionary<string, ContractMetaTxInfo> KnownMetaTxContracts = new ()
        {
            // MANA on Polygon
            { "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4", new ContractMetaTxInfo("Decentraland MANA", "1") },

            // Marketplace V2 on Polygon
            { "0x480a0f4e360e8964e68858dd231c2922f1df45ef", new ContractMetaTxInfo("Decentraland Marketplace", "2") },

            // Bids V2 on Polygon
            { "0xb96697fa4a3361ba35b774a42c58daccaad1b8e1", new ContractMetaTxInfo("Decentraland Bid", "2") },

            // Collection Manager on Polygon
            { "0x9d32aac179153a991e832550d9f96f6d1e05d4b4", new ContractMetaTxInfo("CollectionManager", "2") },
        };

        private record ContractMetaTxInfo(string Name, string Version);

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

        // low-level calls
        private async UniTask<EthApiResponse> SendRpcRequestAsync(EthApiRequest request)
        {
            string rpcUrl = GetRpcUrl((int)chainId);

            var rpcRequest = new
            {
                jsonrpc = "2.0",
                request.id,
                request.method,
                request.@params,
            };

            string requestJson = JsonConvert.SerializeObject(rpcRequest);

            // Send HTTP POST request to RPC endpoint using ThirdwebClient's HTTP client
            IThirdwebHttpClient? httpClient = ThirdWebManager.Instance.Client.HttpClient;

            var content = new System.Net.Http.StringContent(
                requestJson,
                System.Text.Encoding.UTF8,
                "application/json"
            );

            ThirdwebHttpResponseMessage? httpResponse = await httpClient.PostAsync(rpcUrl, content, CancellationToken.None);

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

        // Thirdweb's RPC endpoints
        private static string GetRpcUrl(int chainId) =>
            $"https://{chainId}.rpc.thirdweb.com";

        public event Action<(int code, DateTime expiration, string requestId)>? VerificationRequired;

        public void CancelCurrentWeb3Operation()
        {
            throw new NotImplementedException();
        }

        public async UniTask SubmitOtp(string otp)
        {
            if (pendingWallet == null)
                throw new InvalidOperationException("SubmitOtp called but no pending wallet");

            Debug.Log($"Validating OTP: {otp}");

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

            Debug.Log("Resending OTP");
            await pendingWallet.SendOTP();
            Debug.Log("OTP resent to email");
        }
    }
}
