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
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly int? identityExpirationDuration;

        private TransactionConfirmationDelegate? transactionConfirmationCallback;

        private BigInteger chainId;
        private InAppWallet? pendingWallet;
        private UniTaskCompletionSource<bool>? loginCompletionSource;

        // Minimal ABI with a dummy function to create a base transaction that we'll customize
        // This allows us to support ANY contract call by overriding the data field
        private const string MINIMAL_ABI = @"[{""name"":""execute"",""type"":""function"",""inputs"":[],""outputs"":[]}]";

        public ThirdWebAuthenticator(DecentralandEnvironment environment, IWeb3IdentityCache identityCache, HashSet<string> whitelistMethods,
            HashSet<string> readOnlyMethods, IWeb3AccountFactory web3AccountFactory, int? identityExpirationDuration = null)
        {
            Instance?.Dispose();
            Instance = this;

            this.identityCache = identityCache;
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

        public async UniTask<EthApiResponse> SendAsync(int chainId, EthApiRequest request, CancellationToken ct)
        {
            var targetChainId = new BigInteger(chainId);

            this.chainId = targetChainId;
            await ThirdWebManager.Instance.ActiveWallet.SwitchNetwork(this.chainId);

            return await SendAsync(request, ct);
        }

        public async UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct)
        {
            if (!whitelistMethods.Contains(request.method))
                throw new Web3Exception($"The method is not allowed: {request.method}");

            if (IsReadOnly(request))
                return await SendWithoutConfirmationAsync(request, ct);

            return await SendWithConfirmationAsync(request, ct);
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
        private async UniTask<EthApiResponse> SendWithConfirmationAsync(EthApiRequest request, CancellationToken ct)
        {
            // Request user confirmation before proceeding
            if (transactionConfirmationCallback != null)
            {
                TransactionConfirmationRequest confirmationRequest = CreateConfirmationRequest(request);
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
                return await HandleSendTransactionAsync(request);

            // Fallback for any other non-read-only methods
            throw new Web3Exception($"Unsupported method requiring confirmation: {request.method}");
        }

        /// <summary>
        ///     Creates a confirmation request object with transaction details for the UI
        /// </summary>
        private TransactionConfirmationRequest CreateConfirmationRequest(EthApiRequest request)
        {
            var confirmationRequest = new TransactionConfirmationRequest
            {
                Method = request.method,
                Params = request.@params,
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
            }

            return confirmationRequest;
        }

        private async UniTask<EthApiResponse> HandleSendTransactionAsync(EthApiRequest request)
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
            string txHash = await ExecuteContractCallAsync(to, data, weiValue);

            return new EthApiResponse
            {
                id = request.id,
                jsonrpc = "2.0",
                result = txHash,
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
            catch (InvalidOperationException e) when (e.Message.Contains("invalid or expired")) { throw new CodeVerificationException("Incorrect code", e); }
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
