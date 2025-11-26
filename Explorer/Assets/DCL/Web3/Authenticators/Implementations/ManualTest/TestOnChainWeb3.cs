using System.Numerics;
using Thirdweb;
using ThirdWebUnity;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public class TestOnChainWeb3 : MonoBehaviour
    {
        private string GetNativeCurrencyName(BigInteger chainId)
        {
            // Основные сети
            if (chainId == 1) return "ETH"; // Ethereum Mainnet
            if (chainId == 137) return "MATIC"; // Polygon Mainnet
            if (chainId == 80002) return "MATIC"; // Polygon Amoy Testnet
            if (chainId == 11155111) return "ETH"; // Sepolia Testnet
            if (chainId == 5) return "ETH"; // Goerli Testnet
            if (chainId == 56) return "BNB"; // BSC Mainnet
            if (chainId == 43114) return "AVAX"; // Avalanche C-Chain
            if (chainId == 42161) return "ETH"; // Arbitrum One
            if (chainId == 10) return "ETH"; // Optimism
            if (chainId == 8453) return "ETH"; // Base

            // По умолчанию
            return "ETH";
        }

        [ContextMenu(nameof(TestGetNativeBalance))]
        public async void TestGetNativeBalance()
        {
            string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

            // Получаем chainId чтобы знать какая это сеть
            var chainIdRequest = new EthApiRequest
            {
                id = 1,
                method = "eth_chainId",
                @params = new object[] { },
            };

            EthApiResponse chainIdResponse = await ThirdWebAuthenticator.Instance.SendAsync(chainIdRequest, destroyCancellationToken);
            BigInteger chainId = chainIdResponse.result.ToString().HexToNumber();

            // Определяем название нативной валюты по chainId
            string currencyName = GetNativeCurrencyName(chainId);

            // Получаем баланс
            var balanceRequest = new EthApiRequest
            {
                id = 2,
                method = "eth_getBalance",
                @params = new object[] { walletAddress, "latest" },
            };

            EthApiResponse balanceResponse = await ThirdWebAuthenticator.Instance.SendAsync(balanceRequest, destroyCancellationToken);

            var hexBalance = balanceResponse.result.ToString();

            // Правильная конвертация: Hex → BigInteger → String → Native Currency
            BigInteger weiValue = hexBalance.HexToNumber();
            var weiString = weiValue.ToString();
            string? balance = weiString.ToEth(decimalsToDisplay: 6, addCommas: true);

            Debug.Log($"Chain ID: {chainId}");
            Debug.Log($"Currency: {currencyName}");
            Debug.Log($"Balance (raw hex): {hexBalance}");
            Debug.Log($"Balance (wei): {weiValue}");
            Debug.Log($"Balance: {balance} {currencyName}");
        }

        [ContextMenu(nameof(TestGetERC20Balance))]
        public async void TestGetERC20Balance()
        {
            string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

            var pocContractAddress = "0x4DCEeD47D64299D36a439369C541D48601614159";

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

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);

            var hexBalance = response.result.ToString();

            // Правильная конвертация для ERC20 (обычно 18 decimals, но может быть другое)
            BigInteger tokenAmount = hexBalance.HexToNumber();

            // Для POC токена - проверьте decimals! Предполагаем 18
            decimal pocBalance = (decimal)tokenAmount / 1_000_000_000_000_000_000m;

            Debug.Log($"POC Balance (raw hex): {hexBalance}");
            Debug.Log($"POC Balance (raw amount): {tokenAmount}");
            Debug.Log($"POC Balance: {pocBalance:F6} POC");
        }
    }
}
