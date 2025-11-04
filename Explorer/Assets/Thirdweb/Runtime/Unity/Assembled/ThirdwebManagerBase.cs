using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using UnityEngine;

namespace Thirdweb.Unity
{
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

        [JsonProperty("siweSigner")]
        public IThirdwebWallet SiweSigner;

        [JsonProperty("walletSecret")]
        public string WalletSecret;

        [JsonProperty("forceSiweExternalWalletIds")]
        public List<string> ForceSiweExternalWalletIds;

        [JsonProperty("executionMode")]
        public ExecutionMode ExecutionMode = ExecutionMode.EOA;

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

        [JsonProperty("chainId")]
        public BigInteger ChainId;

        [JsonProperty("inAppWalletOptions")]
        public InAppWalletOptions InAppWalletOptions;

        [JsonProperty("ecosystemWalletOptions", NullValueHandling = NullValueHandling.Ignore)]
        public EcosystemWalletOptions EcosystemWalletOptions;

        [JsonProperty("smartWalletOptions", NullValueHandling = NullValueHandling.Ignore)]
        public SmartWalletOptions SmartWalletOptions;

        [JsonProperty("reownOptions", NullValueHandling = NullValueHandling.Ignore)]
        public ReownOptions ReownOptions;

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

    [Serializable]
    public struct RpcOverride
    {
        public ulong ChainId;
        public string RpcUrl;
    }

    [HelpURL("http://portal.thirdweb.com/unity/v5/thirdwebmanager")]
    public abstract class ThirdwebManagerBase : MonoBehaviour
    {
        [field: SerializeField]
        protected bool InitializeOnAwake { get; set; } = true;

        [field: SerializeField]
        protected bool ShowDebugLogs { get; set; } = true;

        [field: SerializeField]
        protected bool AutoConnectLastWallet { get; set; }

        [field: SerializeField]
        protected string RedirectPageHtmlOverride { get; set; }

        [field: SerializeField]
        protected List<RpcOverride> RpcOverrides { get; set; }

        public IThirdwebWallet ActiveWallet { get; set; }

        public ThirdwebClient Client { get; protected set; }
        public bool Initialized { get; protected set; }

        public static ThirdwebManagerBase Instance { get; protected set; }

        public static readonly string THIRDWEB_UNITY_SDK_VERSION = "6.0.5";

        protected const string THIRDWEB_AUTO_CONNECT_OPTIONS_KEY = "ThirdwebAutoConnectOptions";

        protected Dictionary<string, IThirdwebWallet> WalletMapping;

        public abstract string MobileRedirectScheme { get; }

        protected abstract ThirdwebClient CreateClient();

        // ------------------------------------------------------
        // Lifecycle Methods
        // ------------------------------------------------------

        protected virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            ThirdwebDebug.IsEnabled = ShowDebugLogs;

#if THIRDWEB_REOWN
#if UNITY_6000_0_OR_NEWER
            var reownModalExists = FindAnyObjectByType<Reown.AppKit.Unity.AppKitCore>();
#else
            var reownModalExists = FindObjectOfType<Reown.AppKit.Unity.AppKitCore>();
#endif

            if (!reownModalExists)
            {
                ThirdwebDebug.LogError(
                    "Reown AppKit not found in scene. If you do NOT intend to use ReownWallet, please remove the THIRDWEB_REOWN define symbol from your Player Settings to avoid this error. "
                        + "If you DO intend to use ReownWallet, please drag and drop the \"Reown AppKit\" prefab into the scene. "
                        + "It can be found under Packages/Reown.Appkit.Unity/Prefabs if you installed it correctly from https://docs.reown.com/appkit/unity/core/installation."
                );
            }

#endif

            if (InitializeOnAwake)
                Initialize();
        }

        public virtual async void Initialize()
        {
            Client = CreateClient();

            if (Client == null)
            {
                ThirdwebDebug.LogError("Failed to initialize ThirdwebManager.");
                return;
            }

            ThirdwebDebug.Log("ThirdwebManager initialized.");

            WalletMapping = new Dictionary<string, IThirdwebWallet>();

            if (AutoConnectLastWallet && GetAutoConnectOptions(out WalletOptions lastWalletOptions))
            {
                ThirdwebDebug.Log("Auto-connecting to last wallet.");

                try
                {
                    _ = await ConnectWallet(lastWalletOptions);
                    ThirdwebDebug.Log("Auto-connected to last wallet.");
                }
                catch (Exception e) { ThirdwebDebug.LogError("Failed to auto-connect to last wallet: " + e.Message); }
            }

            Initialized = true;
        }

        // ------------------------------------------------------
        // Contract Methods
        // ------------------------------------------------------

        public virtual async Task<ThirdwebContract> GetContract(string address, BigInteger chainId, string abi = null)
        {
            if (!Initialized)
                throw new InvalidOperationException("ThirdwebManager is not initialized.");

            return await ThirdwebContract.Create(Client, address, chainId, abi);
        }

        // ------------------------------------------------------
        // Connection Methods
        // ------------------------------------------------------
        public async Task<string> RequestEmailThroughModalAsync() =>
            await InAppWalletModal.EmailLogin();

        public virtual async Task<IThirdwebWallet> ConnectWallet(WalletOptions walletOptions)
        {
            if (walletOptions == null)
                throw new ArgumentNullException(nameof(walletOptions));

            if (walletOptions.ChainId <= 0)
                throw new ArgumentException("ChainId must be greater than 0.");

            IThirdwebWallet wallet = null;

            switch (walletOptions.Provider)
            {
                case WalletProvider.InAppWallet:
                    wallet = await InAppWallet.Create(
                        Client,
                        walletOptions.InAppWalletOptions.Email,
                        walletOptions.InAppWalletOptions.PhoneNumber,
                        walletOptions.InAppWalletOptions.AuthProvider,
                        walletOptions.InAppWalletOptions.StorageDirectoryPath,
                        walletOptions.InAppWalletOptions.SiweSigner,
                        walletOptions.InAppWalletOptions.WalletSecret,
                        executionMode: walletOptions.InAppWalletOptions.ExecutionMode
                    );

                    break;
                case WalletProvider.EcosystemWallet:
                    if (walletOptions.EcosystemWalletOptions == null)
                        throw new ArgumentException("EcosystemWalletOptions must be provided for EcosystemWallet provider.");

                    if (string.IsNullOrEmpty(walletOptions.EcosystemWalletOptions.EcosystemId))
                        throw new ArgumentException("EcosystemId must be provided for EcosystemWallet provider.");

                    wallet = await EcosystemWallet.Create(
                        Client,
                        walletOptions.EcosystemWalletOptions.EcosystemId,
                        walletOptions.EcosystemWalletOptions.EcosystemPartnerId,
                        walletOptions.EcosystemWalletOptions.Email,
                        walletOptions.EcosystemWalletOptions.PhoneNumber,
                        walletOptions.EcosystemWalletOptions.AuthProvider,
                        walletOptions.EcosystemWalletOptions.StorageDirectoryPath,
                        walletOptions.EcosystemWalletOptions.SiweSigner,
                        walletOptions.EcosystemWalletOptions.WalletSecret,
                        executionMode: walletOptions.EcosystemWalletOptions.ExecutionMode
                    );

                    break;
                case WalletProvider.ReownWallet:
#if THIRDWEB_REOWN
                    wallet = await ReownWallet.Create(
                        client: this.Client,
                        activeChainId: walletOptions.ChainId,
                        projectId: walletOptions.ReownOptions.ProjectId,
                        name: walletOptions.ReownOptions.Name,
                        description: walletOptions.ReownOptions.Description,
                        url: walletOptions.ReownOptions.Url,
                        iconUrl: walletOptions.ReownOptions.IconUrl,
                        includedWalletIds: walletOptions.ReownOptions.IncludedWalletIds,
                        excludedWalletIds: walletOptions.ReownOptions.ExcludedWalletIds,
                        featuredWalletIds: walletOptions.ReownOptions.FeaturedWalletIds,
                        singleWalletId: walletOptions.ReownOptions.SingleWalletId,
                        tryResumeSession: walletOptions.ReownOptions.TryResumeSession
                    );
                    break;
#else
                    throw new NotSupportedException(
                        "Reown wallet support is not enabled. Please add the THIRDWEB_REOWN Scripting Define symbol in your Player settings to enable it. "
                        + "This assumes you have added Reown Appkit to your packages, installation details can be found here https://docs.reown.com/appkit/unity/core/installation."
                    );
#endif
                default:
                    throw new NotSupportedException($"Wallet provider {walletOptions.Provider} is not supported.");
            }

            // InAppWallet auth flow
            if (walletOptions.Provider == WalletProvider.InAppWallet && !await wallet.IsConnected())
            {
                ThirdwebDebug.Log("Session does not exist or is expired, proceeding with InAppWallet authentication.");

                var inAppWallet = wallet as InAppWallet;

                switch (walletOptions.InAppWalletOptions.AuthProvider)
                {
                    case AuthProvider.Default:
                        await inAppWallet.SendOTP();
                        _ = await InAppWalletModal.LoginWithOtp(inAppWallet);
                        break;
                    case AuthProvider.Siwe:
                        _ = await inAppWallet.LoginWithSiwe(walletOptions.ChainId);
                        break;
                    case AuthProvider.JWT:
                        _ = await inAppWallet.LoginWithJWT(walletOptions.InAppWalletOptions.JwtOrPayload);
                        break;
                    case AuthProvider.AuthEndpoint:
                        _ = await inAppWallet.LoginWithAuthEndpoint(walletOptions.InAppWalletOptions.JwtOrPayload);
                        break;
                    case AuthProvider.Guest:
                        _ = await inAppWallet.LoginWithGuest(SystemInfo.deviceUniqueIdentifier);
                        break;
                    case AuthProvider.Backend:
                        _ = await inAppWallet.LoginWithBackend();
                        break;
                    case AuthProvider.Google:
                    case AuthProvider.Apple:
                    case AuthProvider.Facebook:
                    case AuthProvider.Discord:
                    case AuthProvider.Farcaster:
                    case AuthProvider.Telegram:
                    case AuthProvider.Line:
                    case AuthProvider.X:
                    case AuthProvider.TikTok:
                    case AuthProvider.Coinbase:
                    case AuthProvider.Github:
                    case AuthProvider.Twitch:
                    case AuthProvider.Steam:
                    default:
                        _ = await inAppWallet.LoginWithOauth(
                            IsMobileRuntime(),
                            url => Application.OpenURL(url),
                            MobileRedirectScheme,
                            new CrossPlatformUnityBrowser(RedirectPageHtmlOverride)
                        );

                        break;
                }
            }

            // EcosystemWallet auth flow
            if (walletOptions.Provider == WalletProvider.EcosystemWallet && !await wallet.IsConnected())
            {
                ThirdwebDebug.Log("Session does not exist or is expired, proceeding with EcosystemWallet authentication.");

                var ecosystemWallet = wallet as EcosystemWallet;

                switch (walletOptions.EcosystemWalletOptions.AuthProvider)
                {
                    case AuthProvider.Default:
                        await ecosystemWallet.SendOTP();
                        _ = await EcosystemWalletModal.LoginWithOtp(ecosystemWallet);
                        break;
                    case AuthProvider.Siwe:
                        _ = await ecosystemWallet.LoginWithSiwe(walletOptions.ChainId);
                        break;
                    case AuthProvider.JWT:
                        _ = await ecosystemWallet.LoginWithJWT(walletOptions.EcosystemWalletOptions.JwtOrPayload);
                        break;
                    case AuthProvider.AuthEndpoint:
                        _ = await ecosystemWallet.LoginWithAuthEndpoint(walletOptions.EcosystemWalletOptions.JwtOrPayload);
                        break;
                    case AuthProvider.Guest:
                        _ = await ecosystemWallet.LoginWithGuest(SystemInfo.deviceUniqueIdentifier);
                        break;
                    case AuthProvider.Backend:
                        _ = await ecosystemWallet.LoginWithBackend();
                        break;
                    case AuthProvider.Google:
                    case AuthProvider.Apple:
                    case AuthProvider.Facebook:
                    case AuthProvider.Discord:
                    case AuthProvider.Farcaster:
                    case AuthProvider.Telegram:
                    case AuthProvider.Line:
                    case AuthProvider.X:
                    case AuthProvider.TikTok:
                    case AuthProvider.Coinbase:
                    case AuthProvider.Github:
                    case AuthProvider.Twitch:
                    case AuthProvider.Steam:
                    default:
                        _ = await ecosystemWallet.LoginWithOauth(
                            IsMobileRuntime(),
                            url => Application.OpenURL(url),
                            MobileRedirectScheme,
                            new CrossPlatformUnityBrowser(RedirectPageHtmlOverride)
                        );

                        break;
                }
            }

            string address = await wallet.GetAddress();
            bool isSmartWallet = walletOptions.SmartWalletOptions != null;

            SetAutoConnectOptions(walletOptions);

            // If SmartWallet, do upgrade
            if (isSmartWallet)
            {
                ThirdwebDebug.Log("Upgrading to SmartWallet.");
                return await UpgradeToSmartWallet(wallet, walletOptions.ChainId, walletOptions.SmartWalletOptions);
            }

            ActiveWallet = wallet;
            return wallet;
        }

        public virtual async Task DisconnectWallet()
        {
            if (ActiveWallet != null)
                try { await ActiveWallet.Disconnect(); }
                finally { ActiveWallet = null; }

            PlayerPrefs.DeleteKey(THIRDWEB_AUTO_CONNECT_OPTIONS_KEY);
        }

        public virtual async Task<SmartWallet> UpgradeToSmartWallet(IThirdwebWallet personalWallet, BigInteger chainId, SmartWalletOptions smartWalletOptions)
        {
            if (personalWallet.AccountType == ThirdwebAccountType.SmartAccount)
            {
                ThirdwebDebug.LogWarning("Wallet is already a SmartWallet.");
                return personalWallet as SmartWallet;
            }

            if (smartWalletOptions == null)
                throw new ArgumentNullException(nameof(smartWalletOptions));

            if (chainId <= 0)
                throw new ArgumentException("ChainId must be greater than 0.");

            SmartWallet wallet = await SmartWallet.Create(
                personalWallet,
                chainId,
                smartWalletOptions.SponsorGas,
                smartWalletOptions.FactoryAddress,
                smartWalletOptions.AccountAddressOverride,
                smartWalletOptions.EntryPoint,
                smartWalletOptions.BundlerUrl,
                smartWalletOptions.PaymasterUrl,
                smartWalletOptions.TokenPaymaster
            );

            ActiveWallet = wallet;

            // Persist "smartWalletOptions" to auto-connect
            if (AutoConnectLastWallet && GetAutoConnectOptions(out WalletOptions lastWalletOptions))
            {
                lastWalletOptions.SmartWalletOptions = smartWalletOptions;
                SetAutoConnectOptions(lastWalletOptions);
            }

            return wallet;
        }

        public virtual async Task<List<LinkedAccount>> LinkAccount(IThirdwebWallet mainWallet, IThirdwebWallet walletToLink, string otp = null, BigInteger? chainId = null, string jwtOrPayload = null) =>
            await mainWallet.LinkAccount(
                walletToLink,
                otp,
                IsMobileRuntime(),
                url => Application.OpenURL(url),
                MobileRedirectScheme,
                new CrossPlatformUnityBrowser(RedirectPageHtmlOverride),
                chainId,
                jwtOrPayload,
                jwtOrPayload
            );

        protected virtual bool IsMobileRuntime()
        {
            if (Application.platform == RuntimePlatform.OSXPlayer)
                return true;

            return Application.isMobilePlatform;
        }

        protected virtual bool GetAutoConnectOptions(out WalletOptions lastWalletOptions)
        {
            string connectOptionsStr = PlayerPrefs.GetString(THIRDWEB_AUTO_CONNECT_OPTIONS_KEY, null);

            if (!string.IsNullOrEmpty(connectOptionsStr))
                try
                {
                    lastWalletOptions = JsonConvert.DeserializeObject<WalletOptions>(connectOptionsStr);
                    return true;
                }
                catch
                {
                    ThirdwebDebug.LogWarning("Failed to load last wallet options.");
                    PlayerPrefs.DeleteKey(THIRDWEB_AUTO_CONNECT_OPTIONS_KEY);
                    lastWalletOptions = null;
                    return false;
                }

            lastWalletOptions = null;
            return false;
        }

        protected virtual void SetAutoConnectOptions(WalletOptions walletOptions)
        {
            if (AutoConnectLastWallet)
                try { PlayerPrefs.SetString(THIRDWEB_AUTO_CONNECT_OPTIONS_KEY, JsonConvert.SerializeObject(walletOptions)); }
                catch
                {
                    ThirdwebDebug.LogWarning("Failed to save last wallet options.");
                    PlayerPrefs.DeleteKey(THIRDWEB_AUTO_CONNECT_OPTIONS_KEY);
                }
        }
    }
}
