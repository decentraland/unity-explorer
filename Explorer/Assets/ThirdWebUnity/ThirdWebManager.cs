using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Thirdweb;
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
                httpClient: new ThirdwebHttpClient(),
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
    }
}
