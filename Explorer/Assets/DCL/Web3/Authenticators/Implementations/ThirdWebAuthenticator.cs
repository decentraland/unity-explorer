using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Thirdweb;
using ThirdWebUnity;
using ThirdWebUnity.Playground;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public partial class ThirdWebAuthenticator : IWeb3VerifiedAuthenticator, IVerifiedEthereumApi
    {
        public static ThirdWebAuthenticator Instance;

        private readonly SemaphoreSlim mutex = new (1, 1);

        private readonly HashSet<string> whitelistMethods;
        private readonly IWeb3IdentityCache identityCache;
        private readonly DecentralandEnvironment environment;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly int? identityExpirationDuration;

        private BigInteger chainId;

        public ThirdWebAuthenticator(DecentralandEnvironment environment, IWeb3IdentityCache identityCache, HashSet<string> whitelistMethods,
            IWeb3AccountFactory web3AccountFactory, int? identityExpirationDuration = null)
        {
            Instance?.Dispose();
            Instance = this;

            this.environment = environment;
            this.identityCache = identityCache;
            this.whitelistMethods = whitelistMethods;
            this.web3AccountFactory = web3AccountFactory;
            this.identityExpirationDuration = identityExpirationDuration;

            chainId = EnvChainsUtils.GetChainIdAsInt(environment);
        }

        public async UniTask<IWeb3Identity> LoginAsync(string email, string password, CancellationToken ct)
        {
            await mutex.WaitAsync(ct);

            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                ThirdWebManager.Instance.ActiveWallet
                    = await LoginViaJWT(email, password);

                //  = await LoginViaOTP("popuzin@gmail.com");

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

        private async UniTask<InAppWallet> LoginViaOTP(string email)
        {
            Debug.Log("Login via OTP");

            var walletOptions = new ThirdWebManager.WalletOptions(
                ThirdWebManager.WalletProvider.InAppWallet,
                EnvChainsUtils.GetChainIdAsInt(environment),
                new ThirdWebManager.InAppWalletOptions(authprovider: AuthProvider.Default, email: email)
            );

            InAppWallet wallet = await ThirdWebManager.Instance.CreateInAppWallet(walletOptions);
            await wallet.SendOTP();
            var otp = "MOCK"; // wait callback
            _ = await wallet.LoginWithOtp(otp);
            return wallet;
        }

        private async UniTask<InAppWallet> LoginViaJWT(string email, string password)
        {
            string? jwt = await ThirdWebCustomJWTAuth.GetJWT(email, password);

            var walletOptions = new ThirdWebManager.WalletOptions(
                ThirdWebManager.WalletProvider.InAppWallet,
                EnvChainsUtils.GetChainIdAsInt(environment),
                new ThirdWebManager.InAppWalletOptions(authprovider: AuthProvider.JWT, jwtOrPayload: jwt)
            );

            InAppWallet wallet = await ThirdWebManager.Instance.CreateInAppWallet(walletOptions);
            await wallet.LoginWithJWT(walletOptions.InAppWalletOptions.JwtOrPayload);

            return wallet;
        }

        public void Dispose()
        {
            LogoutAsync(CancellationToken.None).Forget();
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken) =>
            await ThirdWebManager.Instance.DisconnectWallet();

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
            if (string.Equals(request.method, "eth_getBalance"))
            {
                // eth_getBalance params: [address, blockParameter]
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
            if (string.Equals(request.method, "eth_sendTransaction"))
                return await HandleSendTransactionAsync(request);

            // All other RPC methods are read-only (off-chain) - delegate to low-level RPC calls
            return await SendRpcRequestAsync(request);
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

            // For contract interactions with data
            // Pass empty ABI to avoid fetching contract metadata
            ThirdwebContract? contract = await ThirdwebContract.Create(
                ThirdWebManager.Instance.Client,
                to,
                chainId,
                abi: "[]" // Empty ABI - we already have encoded data
            );

            ThirdwebTransaction? transaction = await ThirdwebContract.Prepare(
                ThirdWebManager.Instance.ActiveWallet,
                contract,
                data,
                weiValue
            );

            string? txHash = await ThirdwebTransaction.Send(transaction);

            return new EthApiResponse
            {
                id = request.id,
                jsonrpc = "2.0",
                result = txHash,
            };
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

        // Use ThirdwebClient's RPC endpoint for low-level calls
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

        // Use Thirdweb's RPC endpoints
        private static string GetRpcUrl(int chainId) =>
            $"https://{chainId}.rpc.thirdweb.com";

        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback)
        {
        }

        public void AddVerificationListener(IVerifiedEthereumApi.VerificationDelegate callback)
        {
        }

        // public async UniTask<string> MintFakeManaAsync(decimal amountInMana, CancellationToken ct)
        // {
        //     try
        //     {
        //         // 1. Проверяем, что есть активный кошелёк
        //         IThirdwebWallet? wallet = ThirdWebManager.Instance.ActiveWallet;
        //         if (wallet == null)
        //         {
        //             UnityEngine.Debug.LogError("[UNSPECIFIED]: MintFakeManaAsync: ActiveWallet is null. Call LoginAsync first.");
        //             return string.Empty;
        //         }
        //
        //         // 2. Адрес получателя — текущий EOA пользователя
        //         string toAddress = await wallet.GetAddress();
        //
        //         // 3. Конвертация MANA → wei (18 decimals)
        //         // 1 MANA = 10^18, см. README про FakeMana
        //         // https://github.com/decentraland/governance (Sepolia FakeMana)
        //         BigInteger weiPerMana = BigInteger.Pow(10, 18);
        //         BigInteger amountWei = new BigInteger(amountInMana * (decimal)weiPerMana);
        //
        //         if (amountWei <= BigInteger.Zero)
        //         {
        //             UnityEngine.Debug.LogError($"[UNSPECIFIED]: MintFakeManaAsync: amountInMana must be > 0, got {amountInMana}");
        //             return string.Empty;
        //         }
        //
        //         // 4. Адрес Fake MANA на Sepolia (Sepolia FakeMana из README)
        //         const string fakeManaContractAddress = "0xFa04D2e2BA9aeC166c93dFEEba7427B2303beFa9";
        //
        //         // Минимальный ABI только с методом mint(address to, uint256 amount)
        //         const string fakeManaAbi = @"[
        //             {
        //                 ""inputs"": [
        //                     { ""internalType"": ""address"", ""name"": ""to"", ""type"": ""address"" },
        //                     { ""internalType"": ""uint256"", ""name"": ""amount"", ""type"": ""uint256"" }
        //                 ],
        //                 ""name"": ""mint"",
        //                 ""outputs"": [],
        //                 ""stateMutability"": ""nonpayable"",
        //                 ""type"": ""function""
        //             }
        //         ]";
        //
        //         // 5. Создаём контракт на Sepolia (chainId у тебя уже привязан к Sepolia)
        //         var client = ThirdWebManager.Instance.Client;
        //
        //         ThirdwebContract contract = await ThirdwebContract.Create(
        //             client: client,
        //             address: fakeManaContractAddress,
        //             chain: new BigInteger(11155111),          // private BigInteger chainId => EnvChainsUtils.Sepolia;
        //             abi: fakeManaAbi
        //         );
        //
        //         // 6. Пишем в контракт через ThirdwebContract.Write (без SendTransaction)
        //         var receipt = await contract.Write(wallet, "mint", BigInteger.Zero, toAddress, amountWei);
        //         Console.WriteLine($"Transaction receipt: {receipt}");
        //
        //         UnityEngine.Debug.Log(
        //             $"[MintFakeManaAsync] Minted {amountInMana} Fake MANA to {toAddress}. TxHash: {receipt.TransactionHash}"
        //         );
        //
        //         return receipt.TransactionHash;
        //     }
        //     catch (Exception ex)
        //     {
        //         UnityEngine.Debug.LogError($"[UNSPECIFIED]: MintFakeMana failed: {ex}");
        //         return string.Empty;
        //     }
        // }
    }
}
