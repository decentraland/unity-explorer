using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading;
using Thirdweb;
using Thirdweb.Unity;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Thirdweb InApp Wallet authenticator. Uses email-based login and builds DCL AuthChain with an ephemeral account.
    /// </summary>
    public class ThirdwebInAppWalletAuthenticator : IWeb3VerifiedAuthenticator, IVerifiedEthereumApi
    {
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly DecentralandEnvironment environment;

        // private readonly string loginEmail1 = "popuzin@gmail.com";
        // private readonly string loginEmail2 = "vitaly.popuzin@decentraland.org";
        private IVerifiedEthereumApi.VerificationDelegate? signatureVerificationCallback;

        public ThirdwebInAppWalletAuthenticator(IWeb3AccountFactory web3AccountFactory, DecentralandEnvironment environment)
        {
            this.web3AccountFactory = web3AccountFactory;
            this.environment = environment;
        }

        public void Dispose() { }

        public void AddVerificationListener(IVerifiedEthereumApi.VerificationDelegate callback) =>
            signatureVerificationCallback = callback;

        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            BigInteger chainId = GetChainId(environment);

            // Получаем email из модального окна на сцене
            string loginEmail = await ThirdwebManager.Instance.RequestEmailThroughModalAsync();
            Debug.Log($"VVV ThirdwebAuth: Start login. Email={loginEmail}, Env={environment}, ChainId={chainId}");

            var walletOptions = new WalletOptions(
                WalletProvider.InAppWallet,
                chainId,
                new InAppWalletOptions(loginEmail)
            );

            Debug.Log("VVV ThirdwebAuth: ConnectWallet()");
            IThirdwebWallet wallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);
            Debug.Log("VVV ThirdwebAuth: ConnectWallet() completed");

            string sender = await wallet.GetAddress();
            Debug.Log($"VVV ThirdwebAuth: Wallet address={sender}");

            IWeb3Account ephemeralAccount = web3AccountFactory.CreateRandomAccount();
            DateTime sessionExpiration = DateTime.UtcNow.AddDays(7);

            string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);
            Debug.Log($"VVV ThirdwebAuth: Ephemeral generated. Addr={ephemeralAccount.Address}, Exp={sessionExpiration:s}");

            Debug.Log("VVV ThirdwebAuth: PersonalSign() start");
            string signature = await ThirdwebManager.Instance.ActiveWallet.PersonalSign(ephemeralMessage);
            Debug.Log("VVV ThirdwebAuth: PersonalSign() done");

            var authChain = AuthChain.Create();
            authChain.SetSigner(sender.ToLower());

            authChain.Set(new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = ephemeralMessage,
                signature = signature,
            });

            Debug.Log("VVV ThirdwebAuth: AuthChain built");

            return new DecentralandIdentity(new Web3Address(sender), ephemeralAccount, sessionExpiration, authChain);
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            Debug.Log("VVV ThirdwebAuth: DisconnectWallet()");
            await ThirdwebManager.Instance.DisconnectWallet();
            Debug.Log("VVV ThirdwebAuth: Disconnected");
        }

        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) { }

        public async UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);
            Debug.Log($"VVV ThirdwebEth: SendAsync method={request.method}");

            switch (request.method)
            {
                case "eth_accounts":
                case "eth_requestAccounts":
                {
                    string[] accounts = Array.Empty<string>();

                    if (ThirdwebManager.Instance.ActiveWallet != null)
                        accounts = new[] { await ThirdwebManager.Instance.ActiveWallet.GetAddress() };

                    return new EthApiResponse { id = request.id, jsonrpc = "2.0", result = accounts };
                }
                case "eth_chainId":
                {
                    string chainHex = environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? "0x1" : "0xaa36a7";
                    return new EthApiResponse { id = request.id, jsonrpc = "2.0", result = chainHex };
                }
                case "net_version":
                {
                    string netVersion = environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? "1" : "11155111";
                    return new EthApiResponse { id = request.id, jsonrpc = "2.0", result = netVersion };
                }
                case "eth_signTypedData_v4":
                {
                    string typedJson = ResolveTypedDataJson(request.@params);
                    string sig = await ThirdwebManager.Instance.ActiveWallet.SignTypedDataV4(typedJson);
                    return new EthApiResponse { id = request.id, jsonrpc = "2.0", result = sig };
                }
                case "personal_sign":
                case "eth_sign":
                {
                    string message = ResolvePersonalSignMessage(request.@params);
                    string sig = await ThirdwebManager.Instance.ActiveWallet.PersonalSign(message);
                    return new EthApiResponse { id = request.id, jsonrpc = "2.0", result = sig };
                }
                case "eth_sendTransaction":
                {
                    ThirdwebTransactionInput tx = ResolveSendTransactionInput(request.@params);
                    string txHash = await ThirdwebManager.Instance.ActiveWallet.SendTransaction(tx);
                    return new EthApiResponse { id = request.id, jsonrpc = "2.0", result = txHash };
                }
                default:
                    Debug.Log($"VVV ThirdwebEth: Unsupported method {request.method}");
                    throw new Web3Exception($"Unsupported method: {request.method}");
            }
        }

        private static BigInteger GetChainId(DecentralandEnvironment environment) =>
            environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? new BigInteger(1) : new BigInteger(11155111);

        private static string CreateEphemeralMessage(IWeb3Account ephemeralAccount, DateTime expiration) =>
            $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {expiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

        private static string ResolveTypedDataJson(object[]? @params)
        {
            if (@params == null || @params.Length == 0) throw new Web3Exception("Missing params for eth_signTypedData_v4");
            if (@params.Length >= 2 && @params[1] is string s2) return s2;
            if (@params[0] is string s1) return s1;
            throw new Web3Exception("Cannot resolve typed data JSON");
        }

        private static string ResolvePersonalSignMessage(object[]? @params)
        {
            if (@params == null || @params.Length == 0) throw new Web3Exception("Missing params for personal_sign");
            if (@params[0] is string a && !a.StartsWith("0x")) return a;
            if (@params.Length >= 2 && @params[1] is string b) return b;
            if (@params[0] is string s) return s;
            throw new Web3Exception("Cannot resolve personal_sign message");
        }



        private ThirdwebTransactionInput ResolveSendTransactionInput(object[]? @params)
        {
            if (@params == null || @params.Length == 0)
                throw new Web3Exception("Missing params for eth_sendTransaction");

            JObject txObj = ToJObject(@params[0]);

            string? to = txObj.Value<string>("to");
            string data = txObj.Value<string>("data") ?? txObj.Value<string>("input") ?? "0x";

            long valueWei = 0;
            JToken? valueToken = txObj["value"];

            if (valueToken != null && valueToken.Type != JTokenType.Null)
            {
                string valueString = valueToken.Type == JTokenType.String ? valueToken.Value<string>()! : valueToken.ToString(Formatting.None);
                valueWei = ParseValueToLong(valueString);
            }

            BigInteger chainId = GetChainId(environment);
            return new ThirdwebTransactionInput(chainId, to: to, value: valueWei, data: data);
        }

        private static JObject ToJObject(object o)
        {
            if (o is JObject jo) return jo;

            if (o is string s)
            {
                JObject? parsed = JsonConvert.DeserializeObject<JObject>(s);
                if (parsed != null) return parsed;
            }

            return JObject.FromObject(o);
        }

        private static long ParseValueToLong(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var bi = BigInteger.Parse(value.Substring(2), NumberStyles.AllowHexSpecifier);
                return (long)bi;
            }

            if (long.TryParse(value, out long l)) return l;
            throw new Web3Exception("Invalid value for eth_sendTransaction");
        }
    }
}
