using Newtonsoft.Json;
using System.Collections.Generic;
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
        [SerializeField] private string cryptoReceiver = "0xb1a7fc4bbd9856bfa1f70f6b111444cd9d351592";
        [SerializeField] private float nativeTransferAmount = 0.01f;
        [SerializeField] private float manaTransferAmount = 1f;

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

        [ContextMenu(nameof(TestTransferNative))]
        public async void TestTransferNative()
        {
            if (string.IsNullOrEmpty(cryptoReceiver))
            {
                Debug.LogError("Crypto receiver address is not set!");
                return;
            }

            if (nativeTransferAmount <= 0)
            {
                Debug.LogError("Native transfer amount must be greater than 0!");
                return;
            }

            string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();
            string currencyName = GetNativeCurrencyName(SelectedChainId);

            // Сумма для отправки из Inspector
            decimal amountToSend = (decimal)nativeTransferAmount;
            string weiAmount = amountToSend.ToString().ToWei();

            // Конвертируем в hex для eth_sendTransaction
            BigInteger weiValue = BigInteger.Parse(weiAmount);
            string hexValue = "0x" + weiValue.ToString("X");

            Debug.Log($"Sending {amountToSend} {currencyName} ({weiAmount} wei) from {walletAddress} to {cryptoReceiver}");
            Debug.Log($"Hex value: {hexValue}");

            var txParams = new Dictionary<string, object>
            {
                { "from", walletAddress },
                { "to", cryptoReceiver },
                { "value", hexValue }
            };

            // Сериализуем в JSON для ThirdWebAuthenticator
            string txParamsJson = JsonConvert.SerializeObject(txParams);

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_sendTransaction",
                @params = new object[] { txParamsJson },
            };

            try
            {
                EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(SelectedChainId, request, destroyCancellationToken);
                string txHash = response.result.ToString();

                Debug.Log($"✅ Transaction sent successfully!");
                Debug.Log($"Transaction Hash: {txHash}");
                Debug.Log($"Amount: {amountToSend} {currencyName}");
                Debug.Log($"From: {walletAddress}");
                Debug.Log($"To: {cryptoReceiver}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Transaction failed: {ex.Message}");
            }
        }

        [ContextMenu(nameof(TestTransferMana))]
        public async void TestTransferMana()
        {
            if (string.IsNullOrEmpty(cryptoReceiver))
            {
                Debug.LogError("Crypto receiver address is not set!");
                return;
            }

            if (manaTransferAmount <= 0)
            {
                Debug.LogError("MANA transfer amount must be greater than 0!");
                return;
            }

            string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

            // Получаем адрес контракта MANA
            string? manaContractAddress = GetManaContractAddress(SelectedChainId);

            if (string.IsNullOrEmpty(manaContractAddress))
            {
                Debug.LogError($"MANA contract address not found for Chain ID: {SelectedChainId} ({selectedNetwork})");
                return;
            }

            // Сумма MANA для отправки из Inspector
            decimal manaAmount = (decimal)manaTransferAmount;
            string weiAmount = manaAmount.ToString().ToWei();
            BigInteger weiValue = BigInteger.Parse(weiAmount);

            // ERC20 transfer(address to, uint256 amount) function signature
            string transferSignature = "0xa9059cbb"; // Keccak256("transfer(address,uint256)")[:4]

            // Encode parameters: address (32 bytes) + amount (32 bytes)
            string paddedReceiverAddress = cryptoReceiver.Substring(2).PadLeft(64, '0');
            string paddedAmount = weiValue.ToString("X").PadLeft(64, '0');
            string data = transferSignature + paddedReceiverAddress + paddedAmount;

            Debug.Log($"Transferring {manaAmount} MANA ({weiAmount} smallest units) from {walletAddress} to {cryptoReceiver}");
            Debug.Log($"MANA Contract: {manaContractAddress}");
            Debug.Log($"Data: {data}");

            var txParams = new Dictionary<string, object>
            {
                { "from", walletAddress },
                { "to", manaContractAddress },
                { "value", "0x0" },
                { "data", data }
            };

            // Сериализуем в JSON для ThirdWebAuthenticator
            string txParamsJson = JsonConvert.SerializeObject(txParams);

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_sendTransaction",
                @params = new object[] { txParamsJson },
            };

            try
            {
                EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(SelectedChainId, request, destroyCancellationToken);
                string txHash = response.result.ToString();

                Debug.Log($"✅ MANA transfer sent successfully!");
                Debug.Log($"Transaction Hash: {txHash}");
                Debug.Log($"Amount: {manaAmount} MANA");
                Debug.Log($"From: {walletAddress}");
                Debug.Log($"To: {cryptoReceiver}");
                Debug.Log($"Contract: {manaContractAddress}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ MANA transfer failed: {ex.Message}");
            }
        }
    }
}
