using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Thirdweb;
using Thirdweb.Unity;
using UnityEngine;

namespace ThirdWebUnity
{
    public class ThirdWebManager : MonoBehaviour
    {
        private static readonly string THIRDWEB_UNITY_SDK_VERSION = "6.0.5";

        [field: SerializeField]
        private string ClientId { get; set; }

        [field: SerializeField]
        private string BundleId { get; set; }

        private List<RpcOverride> rpcOverrides;
        public static ThirdWebManager Instance { get; private set; }

        public ThirdwebClient Client { get; private set; }
        public IThirdwebWallet ActiveWallet { get; set; }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            if (string.IsNullOrWhiteSpace(ClientId))
                Debug.LogError("VVV ClientId must be set in order to initialize ThirdwebManager. " + "Get your API key from https://thirdweb.com/create-api-key");

            if (string.IsNullOrWhiteSpace(BundleId))
                Debug.LogError("VVV Bundle Id is not set");

            Client = ThirdwebClient.Create(
                ClientId,
                bundleId: BundleId,
                httpClient: new CrossPlatformUnityHttpClient(),
                sdkName: "UnitySDK",
                sdkOs: Application.platform.ToString(),
                sdkPlatform: "unity",
                sdkVersion: THIRDWEB_UNITY_SDK_VERSION,
                rpcOverrides: rpcOverrides == null || rpcOverrides.Count == 0
                    ? null
                    : rpcOverrides.ToDictionary(rpcOverride => new BigInteger(rpcOverride.ChainId), rpcOverride => rpcOverride.RpcUrl));

            if (Client == null)
                Debug.LogError("VVV Failed to initialize ThirdwebManager.");
        }

        public async Task<IThirdwebWallet> ConnectWallet(IThirdwebWallet wallet, WalletOptions walletOptions)
        {

            // InAppWallet auth flow
            if (walletOptions.Provider == WalletProvider.InAppWallet && !await wallet.IsConnected())
            {
                Debug.Log("VVV InAppWallet authentication (WalletConnect).");

                var inAppWallet = (InAppWallet)wallet;

                switch (walletOptions.InAppWalletOptions.AuthProvider)
                {
                    case AuthProvider.Default:
                        Debug.Log("Sending OTP");
                        await inAppWallet.SendOTP();
                        // Wait OTP form UI ->// _ = await InAppWalletModal.LoginWithOtp(inAppWallet);
                        // Verify with OTP -> //_ = await inAppWallet.LoginWithOtp(otp);
                        break;

                    // case AuthProvider.JWT:
                    //     _ = await inAppWallet.LoginWithJWT(walletOptions.InAppWalletOptions.JwtOrPayload);
                    //     break;
                    // case AuthProvider.AuthEndpoint:
                    //     _ = await inAppWallet.LoginWithAuthEndpoint(walletOptions.InAppWalletOptions.JwtOrPayload);
                    //     break;
                    // case AuthProvider.Google:
                    // case AuthProvider.Apple:
                    // case AuthProvider.Facebook:
                    // case AuthProvider.Discord:
                    // case AuthProvider.Farcaster:
                    // case AuthProvider.Telegram:
                    // case AuthProvider.Line:
                    // case AuthProvider.X:
                    // case AuthProvider.TikTok:
                    // case AuthProvider.Coinbase:
                    // case AuthProvider.Github:
                    // case AuthProvider.Twitch:
                    // case AuthProvider.Steam:
                    // default:
                    //     _ = await inAppWallet.LoginWithOauth(
                    //         isMobile: false,
                    //         url => Application.OpenURL(url),
                    //         mobileRedirectScheme: string.Empty,
                    //         new CrossPlatformUnityBrowser(RedirectPageHtmlOverride)
                    //     );
                    //     break;
                }
            }

            ActiveWallet = wallet;
            return wallet;
        }

        public async Task<InAppWallet> CreateInAppWallet(WalletOptions walletOptions)
        {
            if (walletOptions == null)
                throw new ArgumentNullException(nameof(walletOptions));

            if (walletOptions.ChainId <= 0)
                throw new ArgumentException("ChainId must be greater than 0.");

            InAppWallet wallet = await InAppWallet.Create(
                Client,
                walletOptions.InAppWalletOptions.Email,
                walletOptions.InAppWalletOptions.PhoneNumber,
                walletOptions.InAppWalletOptions.AuthProvider,
                walletOptions.InAppWalletOptions.StorageDirectoryPath,
                walletOptions.InAppWalletOptions.SiweSigner,
                walletOptions.InAppWalletOptions.WalletSecret,
                executionMode: walletOptions.InAppWalletOptions.ExecutionMode
            );

            return wallet;
        }

        public virtual async Task DisconnectWallet()
        {
            if (ActiveWallet != null)
                try { await ActiveWallet.Disconnect(); }
                finally { ActiveWallet = null; }

            // PlayerPrefs.DeleteKey(THIRDWEB_AUTO_CONNECT_OPTIONS_KEY);
        }

#region Wallet Options
        [Serializable]
        public struct RpcOverride
        {
            public ulong ChainId;
            public string RpcUrl;
        }

        [Serializable]
        public enum WalletProvider
        {
            InAppWallet,
            EcosystemWallet,
            ReownWallet,
        }

        [Serializable]
        public class InAppWalletOptions : EcosystemWalletOptions
        {
            public InAppWalletOptions(
                string email = null,
                string phoneNumber = null,
                AuthProvider authprovider = AuthProvider.Default,
                string jwtOrPayload = null,
                string storageDirectoryPath = null,
                IThirdwebWallet siweSigner = null,
                string walletSecret = null,
                List<string> forceSiweExternalWalletIds = null,
                ExecutionMode executionMode = ExecutionMode.EOA
            )
                : base(
                    email: email,
                    phoneNumber: phoneNumber,
                    authprovider: authprovider,
                    jwtOrPayload: jwtOrPayload,
                    storageDirectoryPath: storageDirectoryPath,
                    siweSigner: siweSigner,
                    walletSecret: walletSecret,
                    forceSiweExternalWalletIds: forceSiweExternalWalletIds,
                    executionMode: executionMode
                ) { }
        }

        [Serializable]
        public class EcosystemWalletOptions
        {
            [JsonProperty("ecosystemId")]
            public string EcosystemId;

            [JsonProperty("ecosystemPartnerId")]
            public string EcosystemPartnerId;

            [JsonProperty("email")]
            public string Email;

            [JsonProperty("phoneNumber")]
            public string PhoneNumber;

            [JsonProperty("authProvider")]
            public AuthProvider AuthProvider;

            [JsonProperty("jwtOrPayload")]
            public string JwtOrPayload;

            [JsonProperty("storageDirectoryPath")]
            public string StorageDirectoryPath;

            [JsonProperty("walletSecret")]
            public string WalletSecret;

            [JsonProperty("forceSiweExternalWalletIds")]
            public List<string> ForceSiweExternalWalletIds;

            [JsonProperty("executionMode")]
            public ExecutionMode ExecutionMode = ExecutionMode.EOA;

            [JsonProperty("siweSigner")]
            public IThirdwebWallet SiweSigner;

            public EcosystemWalletOptions(
                string ecosystemId = null,
                string ecosystemPartnerId = null,
                string email = null,
                string phoneNumber = null,
                AuthProvider authprovider = AuthProvider.Default,
                string jwtOrPayload = null,
                string storageDirectoryPath = null,
                IThirdwebWallet siweSigner = null,
                string walletSecret = null,
                List<string> forceSiweExternalWalletIds = null,
                ExecutionMode executionMode = ExecutionMode.EOA
            )
            {
                EcosystemId = ecosystemId;
                EcosystemPartnerId = ecosystemPartnerId;
                Email = email;
                PhoneNumber = phoneNumber;
                AuthProvider = authprovider;
                JwtOrPayload = jwtOrPayload;
                StorageDirectoryPath = storageDirectoryPath ?? Path.Combine(Application.persistentDataPath, "Thirdweb", "EcosystemWallet");
                SiweSigner = siweSigner;
                WalletSecret = walletSecret;
                ForceSiweExternalWalletIds = forceSiweExternalWalletIds;
                ExecutionMode = executionMode;
            }
        }

        [Serializable]
        public class SmartWalletOptions
        {
            [JsonProperty("sponsorGas")]
            public bool SponsorGas;

            [JsonProperty("factoryAddress")]
            public string FactoryAddress;

            [JsonProperty("accountAddressOverride")]
            public string AccountAddressOverride;

            [JsonProperty("entryPoint")]
            public string EntryPoint;

            [JsonProperty("bundlerUrl")]
            public string BundlerUrl;

            [JsonProperty("paymasterUrl")]
            public string PaymasterUrl;

            [JsonProperty("tokenPaymaster")]
            public TokenPaymaster TokenPaymaster;

            public SmartWalletOptions(
                bool sponsorGas,
                string factoryAddress = null,
                string accountAddressOverride = null,
                string entryPoint = null,
                string bundlerUrl = null,
                string paymasterUrl = null,
                TokenPaymaster tokenPaymaster = TokenPaymaster.NONE
            )
            {
                SponsorGas = sponsorGas;
                FactoryAddress = factoryAddress;
                AccountAddressOverride = accountAddressOverride;
                EntryPoint = entryPoint;
                BundlerUrl = bundlerUrl;
                PaymasterUrl = paymasterUrl;
                TokenPaymaster = tokenPaymaster;
            }
        }

        [Serializable]
        public class ReownOptions
        {
            [JsonProperty("projectId")]
            public string ProjectId;

            [JsonProperty("name")]
            public string Name;

            [JsonProperty("description")]
            public string Description;

            [JsonProperty("url")]
            public string Url;

            [JsonProperty("iconUrl")]
            public string IconUrl;

            [JsonProperty("includedWalletIds")]
            public string[] IncludedWalletIds;

            [JsonProperty("excludedWalletIds")]
            public string[] ExcludedWalletIds;

            [JsonProperty("featuredWalletIds")]
            public string[] FeaturedWalletIds;

            [JsonProperty("singleWalletId")]
            public string SingleWalletId;

            [JsonProperty("tryResumeSession")]
            public bool TryResumeSession;

            public ReownOptions(
                string projectId = null,
                string name = null,
                string description = null,
                string url = null,
                string iconUrl = null,
                string[] includedWalletIds = null,
                string[] excludedWalletIds = null,
                string[] featuredWalletIds = null,
                string singleWalletId = null,
                bool tryResumeSession = true
            )
            {
                if (singleWalletId != null && (includedWalletIds != null || excludedWalletIds != null || featuredWalletIds != null))
                    throw new ArgumentException("singleWalletId cannot be used with includedWalletIds, excludedWalletIds, or featuredWalletIds.");

                ProjectId = projectId ?? "35603765088f9ed24db818100fdbb6f9";
                Name = name ?? "thirdweb";
                Description = description ?? "thirdweb powered game";
                Url = url ?? "https://thirdweb.com";
                IconUrl = iconUrl ?? "https://thirdweb.com/favicon.ico";
                IncludedWalletIds = includedWalletIds;
                ExcludedWalletIds = excludedWalletIds;
                FeaturedWalletIds = featuredWalletIds;
                SingleWalletId = singleWalletId;
                TryResumeSession = tryResumeSession;
            }
        }

        [Serializable]
        public class WalletOptions
        {
            [JsonProperty("provider")]
            public WalletProvider Provider;

            [JsonProperty("inAppWalletOptions")]
            public InAppWalletOptions InAppWalletOptions;

            [JsonProperty("ecosystemWalletOptions", NullValueHandling = NullValueHandling.Ignore)]
            public EcosystemWalletOptions EcosystemWalletOptions;

            [JsonProperty("smartWalletOptions", NullValueHandling = NullValueHandling.Ignore)]
            public SmartWalletOptions SmartWalletOptions;

            [JsonProperty("reownOptions", NullValueHandling = NullValueHandling.Ignore)]
            public ReownOptions ReownOptions;

            [JsonProperty("chainId")]
            public BigInteger ChainId;

            public WalletOptions(
                WalletProvider provider,
                BigInteger chainId,
                InAppWalletOptions inAppWalletOptions = null,
                EcosystemWalletOptions ecosystemWalletOptions = null,
                SmartWalletOptions smartWalletOptions = null,
                ReownOptions reownOptions = null
            )
            {
                Provider = provider;
                ChainId = chainId;
                InAppWalletOptions = inAppWalletOptions ?? new InAppWalletOptions();
                SmartWalletOptions = smartWalletOptions;
                EcosystemWalletOptions = ecosystemWalletOptions;
                ReownOptions = reownOptions ?? new ReownOptions();
            }
        }
#endregion
    }
}
