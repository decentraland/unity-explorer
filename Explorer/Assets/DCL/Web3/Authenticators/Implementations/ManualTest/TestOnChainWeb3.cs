using System.Numerics;
using Thirdweb;
using ThirdWebUnity;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public enum ChainNetwork
    {
        EthereumMainnet = 1,
        Goerli = 5,
        Sepolia = 11155111,
        PolygonMainnet = 137,
        Mumbai = 80001,
        Amoy = 80002,
    }

    public class TestOnChainWeb3 : MonoBehaviour
    {
        [SerializeField] private ChainNetwork selectedNetwork = ChainNetwork.Amoy;

        private int SelectedChainId => (int)selectedNetwork;

        private static string GetNativeCurrencyName(int chainId) =>
            chainId switch
            {
                // Ethereum сети
                1 => "ETH", // Ethereum Mainnet
                11155111 => "ETH", // Sepolia Testnet
                5 => "ETH", // Goerli Testnet

                // Polygon сети
                137 => "MATIC", //  Polygon Mainnet
                80002 => "MATIC", //  Amoy Testnet
                80001 => "MATIC", //  Mumbai Testnet

                // Другие сети
                56 => "BNB", // BSC Mainnet
                43114 => "AVAX", // Avalanche C-Chain
                42161 => "ETH", // Arbitrum One
                10 => "ETH", // Optimism
                8453 => "ETH", // Base

                _ => "ETH", // По умолчанию
            };

        private static string? GetManaContractAddress(int chainId)
        {
            return chainId switch
                   {
                       // Ethereum сети
                       1 => "0x0f5d2fb29fb7d3cfee444a200298f468908cc942", //  Mainnet
                       11155111 => "0xfa04d2e2ba9aec166c93dfeeba7427b2303befa9", //  Sepolia
                       5 => "0xe7fDae84ACaba2A5Ba817B6E6D8A2d415DBFEdbe", //  Goerli

                       // Polygon сети
                       137 => "0xA1c57f48F0Deb89f569dFbE6E2B7f46D33606fD4", //  MATIC (Polygon Mainnet)
                       80002 => "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0", //  Amoy
                       80001 => "0x882Da5967c435eA5cC6b09150d55E8304B838f45", //  Mumbai

                       // Неизвестная сеть
                       _ => null,
                   };
        }

        [ContextMenu(nameof(TestGetNativeBalance))]
        public async void TestGetNativeBalance()
        {
            string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

            // Определяем название нативной валюты по выбранному chainId
            string currencyName = GetNativeCurrencyName(SelectedChainId);

            // Получаем баланс
            var balanceRequest = new EthApiRequest
            {
                id = 1,
                method = "eth_getBalance",
                @params = new object[] { walletAddress, "latest" },
            };

            EthApiResponse balanceResponse = await ThirdWebAuthenticator.Instance.SendAsync(SelectedChainId, balanceRequest, destroyCancellationToken);

            var hexBalance = balanceResponse.result.ToString();

            // Правильная конвертация: Hex → BigInteger → String → Native Currency
            BigInteger weiValue = hexBalance.HexToNumber();
            var weiString = weiValue.ToString();
            string? balance = weiString.ToEth(decimalsToDisplay: 6, addCommas: true);

            Debug.Log($"Chain ID: {SelectedChainId} ({selectedNetwork})");
            Debug.Log($"Currency: {currencyName}");
            Debug.Log($"Balance (raw hex): {hexBalance}");
            Debug.Log($"Balance (wei): {weiValue}");
            Debug.Log($"Balance: {balance} {currencyName}");
        }

        [ContextMenu(nameof(TestGetERC20Balance))]
        public async void TestGetERC20Balance()
        {
            string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

            // Выбираем адрес контракта MANA в зависимости от выбранной сети
            string? pocContractAddress = GetManaContractAddress(SelectedChainId);

            if (string.IsNullOrEmpty(pocContractAddress))
            {
                Debug.LogError($"MANA contract address not found for Chain ID: {SelectedChainId} ({selectedNetwork})");
                return;
            }

            Debug.Log($"Using MANA contract: {pocContractAddress} on Chain ID: {SelectedChainId} ({selectedNetwork})");

            // ERC20 balanceOf(address) function signature
            var balanceOfSignature = "0x70a08231"; // Keccak256("balanceOf(address)")[:4]

            // Encode wallet address (pad to 32 bytes)
            string paddedAddress = walletAddress.Substring(2).PadLeft(64, '0');
            string data = balanceOfSignature + paddedAddress;

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_call",
                @params = new object[]
                {
                    new
                    {
                        to = pocContractAddress,
                        data,
                    },
                    "latest",
                },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(SelectedChainId, request, destroyCancellationToken);

            var hexBalance = response.result.ToString();

            // Правильная конвертация для ERC20 (обычно 18 decimals для MANA)
            BigInteger tokenAmount = hexBalance.HexToNumber();

            // Для MANA токена - 18 decimals
            decimal manaBalance = (decimal)tokenAmount / 1_000_000_000_000_000_000m;

            Debug.Log($"MANA Balance (raw hex): {hexBalance}");
            Debug.Log($"MANA Balance (raw amount): {tokenAmount}");
            Debug.Log($"MANA Balance: {manaBalance:F6} MANA");
        }

        // [ContextMenu("Test Mint Fake MANA")]
        // public async void TestMintFakeMana()
        // {
        //     try
        //     {
        //         // например, минтим 10 MANA
        //         decimal amount = 10m;
        //
        //         string txHash = await ThirdWebAuthenticator.Instance
        //            .MintFakeManaAsync(amount, CancellationToken.None);
        //
        //         Debug.Log($"Fake MANA mint tx hash: {txHash}");
        //     }
        //     catch (System.Exception e)
        //     {
        //         Debug.LogError($"MintFakeMana failed: {e}");
        //     }
        // }
    }
}
