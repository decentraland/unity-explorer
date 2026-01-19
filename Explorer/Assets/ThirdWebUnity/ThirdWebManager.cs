using DCL.Prefs;
using System.Collections.Generic;
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
                rpcOverrides: new Dictionary<BigInteger, string>
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
                });

            if (Client == null)
                Debug.LogError("VVV Failed to initialize ThirdwebManager.");
        }

        public virtual async Task DisconnectWallet()
        {
            if (ActiveWallet != null)
                try { await ActiveWallet.Disconnect(); }
                finally { ActiveWallet = null; }

            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.LOGGEDIN_EMAIL);
        }
    }
}
