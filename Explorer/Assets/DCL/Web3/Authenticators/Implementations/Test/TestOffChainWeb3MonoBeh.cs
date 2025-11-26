using DCL.Web3.Abstract;
using System.Numerics;
using Thirdweb;
using ThirdWebUnity;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public class TestOffChainWeb3MonoBeh : MonoBehaviour
    {
        [ContextMenu(nameof(GetAddress))]
        public async void GetAddress()
        {
            string address = await ThirdWebManager.Instance.ActiveWallet.GetAddress();
            Debug.Log($"Wallet Address: {address}");
        }

        [ContextMenu(nameof(TestNetVersion))]
        public async void TestNetVersion()
        {
            var request = new EthApiRequest
            {
                id = 1,
                method = "net_version",
                @params = new object[] { },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);
            Debug.Log($"net_version: {response.result}");
        }

        [ContextMenu(nameof(TestWeb3ClientVersion))]
        public async void TestWeb3ClientVersion()
        {
            var request = new EthApiRequest
            {
                id = 1,
                method = "web3_clientVersion",
                @params = new object[] { },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);
            Debug.Log($"web3_clientVersion: {response.result}");
        }

        [ContextMenu(nameof(TestEthGetTransactionReceipt))]
        public async void TestEthGetTransactionReceipt()
        {
            // Пример txHash - замените на реальный хеш транзакции
            var txHash = "0x0000000000000000000000000000000000000000000000000000000000000000";

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_getTransactionReceipt",
                @params = new object[] { txHash },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);
            Debug.Log($"eth_getTransactionReceipt: {response.result}");
        }

        [ContextMenu(nameof(TestEthEstimateGas))]
        public async void TestEthEstimateGas()
        {
            string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

            var txObject = new
            {
                from = walletAddress,
                to = "0x0000000000000000000000000000000000000000",
                value = "0x0",
                data = "0x",
            };

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_estimateGas",
                @params = new object[] { txObject },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);

            var hex = response.result.ToString();
            BigInteger gasAmount = hex.HexToNumber();
            Debug.Log($"eth_estimateGas: {gasAmount} gas ({hex})");
        }

        [ContextMenu(nameof(TestEthGetStorageAt))]
        public async void TestEthGetStorageAt()
        {
            // Пример: читаем storage slot 0 у нулевого адреса
            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_getStorageAt",
                @params = new object[] { "0x0000000000000000000000000000000000000000", "0x0", "latest" },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);
            Debug.Log($"eth_getStorageAt: {response.result}");
        }

        [ContextMenu(nameof(TestEthBlockNumber))]
        public async void TestEthBlockNumber()
        {
            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_blockNumber",
                @params = new object[] { },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);

            var hex = response.result.ToString();
            BigInteger blockNumber = hex.HexToNumber();
            Debug.Log($"eth_blockNumber: {blockNumber} ({hex})");
        }

        [ContextMenu(nameof(TestEthGasPrice))]
        public async void TestEthGasPrice()
        {
            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_gasPrice",
                @params = new object[] { },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);

            var hex = response.result.ToString();
            BigInteger weiPrice = hex.HexToNumber();

            // Конвертируем в Gwei (1 Gwei = 10^9 Wei)
            BigInteger gweiPrice = weiPrice / 1_000_000_000;

            // Используем ThirdWeb ToEth для форматирования
            string ethPrice = hex.ToEth(decimalsToDisplay: 9, addCommas: false);

            Debug.Log($"eth_gasPrice: {gweiPrice} Gwei ({ethPrice} ETH)");
        }

        [ContextMenu(nameof(TestEthProtocolVersion))]
        public async void TestEthProtocolVersion()
        {
            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_protocolVersion",
                @params = new object[] { },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);
            Debug.Log($"eth_protocolVersion: {response.result}");
        }

        [ContextMenu(nameof(TestWeb3Sha3))]
        public async void TestWeb3Sha3()
        {
            // Хешируем строку "Hello World"
            var request = new EthApiRequest
            {
                id = 1,
                method = "web3_sha3",
                @params = new object[] { "0x48656c6c6f20576f726c64" }, // "Hello World" в hex
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);
            Debug.Log($"web3_sha3: {response.result}");
        }

        [ContextMenu(nameof(TestEthGetTransactionCount))]
        public async void TestEthGetTransactionCount()
        {
            string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_getTransactionCount",
                @params = new object[] { walletAddress, "latest" },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);

            var hex = response.result.ToString();
            BigInteger nonce = hex.HexToNumber();
            Debug.Log($"eth_getTransactionCount (nonce): {nonce} ({hex})");
        }

        [ContextMenu(nameof(TestEthGetBlockByNumber))]
        public async void TestEthGetBlockByNumber()
        {
            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_getBlockByNumber",
                @params = new object[] { "latest", false }, // false = без полных транзакций
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);
            Debug.Log($"eth_getBlockByNumber: {response.result}");
        }

        [ContextMenu(nameof(TestEthGetCode))]
        public async void TestEthGetCode()
        {
            // Пример: получаем код контракта (если адрес контракта)
            var contractAddress = "0x0000000000000000000000000000000000000000";

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_getCode",
                @params = new object[] { contractAddress, "latest" },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);

            var code = response.result.ToString();
            byte[] codeBytes = code.HexToBytes();
            Debug.Log($"eth_getCode: {codeBytes.Length} bytes ({code})");
        }

        [ContextMenu(nameof(TestEthRequestAccounts))]
        public async void TestEthRequestAccounts()
        {
            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_requestAccounts",
                @params = new object[] { },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);
            Debug.Log($"eth_requestAccounts: {response.result}");
        }
    }
}
