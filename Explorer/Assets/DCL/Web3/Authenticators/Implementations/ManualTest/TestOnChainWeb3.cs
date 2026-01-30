using DCL.Backpack.Gifting.Utils;
using DCL.Web3.Authenticators.ManualTest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Numerics;
using Thirdweb;
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

        [ContextMenu(nameof(SetSepoliaChain))]
        private void SetSepoliaChain()
        {
            ThirdWebTestHelper.GetAuthenticator()?.SetSepoliaChain();
        }

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
            string walletAddress = await ThirdWebTestHelper.GetActiveWallet()!.GetAddress();

            // Определяем название нативной валюты по выбранному chainId
            string currencyName = GetNativeCurrencyName(SelectedChainId);

            // Получаем баланс
            var balanceRequest = new EthApiRequest
            {
                id = 1,
                method = "eth_getBalance",
                @params = new object[] { walletAddress, "latest" },
            };

            EthApiResponse balanceResponse = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(SelectedChainId, balanceRequest, destroyCancellationToken);

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
            string walletAddress = await ThirdWebTestHelper.GetActiveWallet()!.GetAddress();

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

            EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(SelectedChainId, request, destroyCancellationToken);

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

            string walletAddress = await ThirdWebTestHelper.GetActiveWallet()!.GetAddress();
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
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(SelectedChainId, request, destroyCancellationToken);
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

            string walletAddress = await ThirdWebTestHelper.GetActiveWallet()!.GetAddress();

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
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(SelectedChainId, request, destroyCancellationToken);
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

        /// <summary>
        ///     Проверяет domain separator контракта и сравнивает с нашим.
        ///     Тестирует ВСЕ возможные форматы EIP712Domain.
        /// </summary>
        [ContextMenu(nameof(CheckDomainSeparator))]
        public async void CheckDomainSeparator()
        {
            string contractAddress = testContractAddress;
            const int chainId = 137; // Polygon Mainnet

            Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
            Debug.Log("║  DOMAIN SEPARATOR COMPREHENSIVE TEST                              ║");
            Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
            Debug.Log($"Contract: {contractAddress}");
            Debug.Log($"Chain ID: {chainId}");

            // First, get the contract name
            var contractName = "";

            try
            {
                var nameRequest = new EthApiRequest
                {
                    id = 1,
                    method = "eth_call",
                    @params = new object[]
                    {
                        new { to = contractAddress, data = "0x06fdde03" }, // name()
                        "latest",
                    },
                };

                EthApiResponse nameResponse = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(chainId, nameRequest, destroyCancellationToken);
                string nameHex = nameResponse.result?.ToString() ?? "0x";

                // Decode string
                if (nameHex.Length > 130)
                {
                    string clean = nameHex.StartsWith("0x") ? nameHex.Substring(2) : nameHex;
                    var length = System.Convert.ToInt32(clean.Substring(64, 64), 16);
                    string dataHex = clean.Substring(128, length * 2);
                    var bytes = new byte[dataHex.Length / 2];

                    for (var i = 0; i < bytes.Length; i++)
                        bytes[i] = System.Convert.ToByte(dataHex.Substring(i * 2, 2), 16);

                    contractName = System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                }

                Debug.Log($"Contract name: '{contractName}'");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to get contract name: {ex.Message}");
                contractName = "Unknown";
            }

            // Get contract's domain separator
            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_call",
                @params = new object[]
                {
                    new { to = contractAddress, data = "0xf698da25" }, // domainSeparator()
                    "latest",
                },
            };

            var contractDomainSep = "";

            try
            {
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(chainId, request, destroyCancellationToken);
                contractDomainSep = response.result?.ToString() ?? "0x";
            }
            catch (System.Exception ex) { Debug.LogWarning($"domainSeparator() failed: {ex.Message}"); }

            Debug.Log("");
            Debug.Log($"★★★ CONTRACT DOMAIN SEPARATOR: {contractDomainSep}");
            Debug.Log("");

            // Test ALL possible formats
            Debug.Log("Testing all EIP712Domain formats...");
            Debug.Log("");

            var found = false;

            // Format 1: DCL/Matic with salt (our current implementation)
            // EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)
            foreach (string version in new[] { "1", "2" })
            {
                string sep = ThirdWebAuthenticator.ComputeDomainSeparator(contractName, version, contractAddress, chainId);
                bool match = contractDomainSep.Equals(sep, System.StringComparison.OrdinalIgnoreCase);
                Debug.Log($"[Salt format, v='{version}'] {sep} {(match ? "✅ MATCH!" : "")}");
                if (match) found = true;
            }

            Debug.Log("");

            // Format 2: Standard EIP-712 with chainId
            // EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)
            foreach (string version in new[] { "1", "2" })
            {
                string sep = ThirdWebAuthenticator.ComputeDomainSeparatorStandard(contractName, version, contractAddress, chainId);
                bool match = contractDomainSep.Equals(sep, System.StringComparison.OrdinalIgnoreCase);
                Debug.Log($"[Standard format, v='{version}'] {sep} {(match ? "✅ MATCH!" : "")}");
                if (match) found = true;
            }

            Debug.Log("");

            // Format 3: Minimal (no verifyingContract)
            foreach (string version in new[] { "1", "2" })
            {
                string sep = ThirdWebAuthenticator.ComputeDomainSeparatorMinimal(contractName, version, chainId);
                bool match = contractDomainSep.Equals(sep, System.StringComparison.OrdinalIgnoreCase);
                Debug.Log($"[Minimal format, v='{version}'] {sep} {(match ? "✅ MATCH!" : "")}");
                if (match) found = true;
            }

            Debug.Log("");

            // Format 4: DCL Collection contracts use HARDCODED name "Decentraland Collection"!
            // See: ERC721BaseCollectionV2.sol -> _initializeEIP712('Decentraland Collection', '2')
            Debug.Log("Testing with HARDCODED EIP712 name 'Decentraland Collection'...");
            const string DCL_COLLECTION_NAME = "Decentraland Collection";

            foreach (string version in new[] { "1", "2" })
            {
                string sep = ThirdWebAuthenticator.ComputeDomainSeparator(DCL_COLLECTION_NAME, version, contractAddress, chainId);
                bool match = contractDomainSep.Equals(sep, System.StringComparison.OrdinalIgnoreCase);
                Debug.Log($"[DCL Collection, v='{version}'] {sep} {(match ? "✅ MATCH!" : "")}");

                if (match)
                {
                    found = true;
                    Debug.Log($"🎯 FOUND! Use name='{DCL_COLLECTION_NAME}', version='{version}' for ALL DCL collection contracts!");
                }
            }

            Debug.Log("");

            if (!found)
            {
                Debug.LogError("❌ NO FORMAT MATCHED! Contract uses unknown EIP712Domain format.");
                Debug.Log("");
                Debug.Log("Possible reasons:");
                Debug.Log("1. Contract name encoding differs (UTF-8 vs ASCII?)");
                Debug.Log("2. Contract uses a different domain type string");
                Debug.Log("3. Contract was deployed with different chainId");
                Debug.Log("");
                Debug.Log("Try checking contract source code on Polygonscan.");
            }
            else { Debug.Log("✅ Found matching format! Update ThirdWebAuthenticator to use it."); }

            Debug.Log("══════════════════════════════════════════════════════════════════");

            // Also check what chainId the contract thinks it has
            Debug.Log("");
            Debug.Log("Checking contract's getChainId()...");

            try
            {
                var chainIdRequest = new EthApiRequest
                {
                    id = 1,
                    method = "eth_call",
                    @params = new object[]
                    {
                        new { to = contractAddress, data = "0x3408e470" }, // getChainId()
                        "latest",
                    },
                };

                EthApiResponse chainIdResponse = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(chainId, chainIdRequest, destroyCancellationToken);
                string chainIdHex = chainIdResponse.result?.ToString() ?? "0x0";
                BigInteger contractChainId = chainIdHex.HexToNumber();
                Debug.Log($"Contract's getChainId(): {contractChainId}");

                if (contractChainId != chainId)
                {
                    Debug.LogError($"❌ CHAIN ID MISMATCH! Contract thinks it's on chain {contractChainId}, but we're using {chainId}");
                    Debug.Log("This could explain the domain separator mismatch!");
                }
                else { Debug.Log($"✅ Chain ID matches: {chainId}"); }
            }
            catch (System.Exception ex) { Debug.LogWarning($"getChainId() failed: {ex.Message}"); }
        }

        /// <summary>
        ///     Проверяет domain separator для Test Case 2 контракта.
        /// </summary>
        [ContextMenu(nameof(CheckDomainSeparator2))]
        public async void CheckDomainSeparator2()
        {
            string contractAddress = testContractAddress2;
            const int chainId = 137; // Polygon Mainnet

            Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
            Debug.Log("║  DOMAIN SEPARATOR TEST - Case 2                                   ║");
            Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
            Debug.Log($"Contract: {contractAddress}");
            Debug.Log($"Chain ID: {chainId}");

            // Get contract's domain separator
            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_call",
                @params = new object[]
                {
                    new { to = contractAddress, data = "0xf698da25" }, // domainSeparator()
                    "latest",
                },
            };

            var contractDomainSep = "";

            try
            {
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(chainId, request, destroyCancellationToken);
                contractDomainSep = response.result?.ToString() ?? "0x";
            }
            catch (System.Exception ex) { Debug.LogWarning($"domainSeparator() failed: {ex.Message}"); }

            Debug.Log("");
            Debug.Log($"★★★ CONTRACT DOMAIN SEPARATOR: {contractDomainSep}");
            Debug.Log("");

            // Test with DCL Collection hardcoded values
            Debug.Log("Testing with DCL Collection EIP-712 domain (name='Decentraland Collection', version='2')...");
            const string DCL_COLLECTION_NAME = "Decentraland Collection";
            string sep = ThirdWebAuthenticator.ComputeDomainSeparator(DCL_COLLECTION_NAME, "2", contractAddress, chainId);
            bool match = contractDomainSep.Equals(sep, System.StringComparison.OrdinalIgnoreCase);
            Debug.Log($"[DCL Collection, v='2'] {sep} {(match ? "✅ MATCH!" : "❌ NO MATCH")}");

            if (match) { Debug.Log($"🎯 SUCCESS! Domain separator matches with name='{DCL_COLLECTION_NAME}', version='2'"); }
            else { Debug.LogError("❌ Domain separator does not match. Check contract source code."); }
        }

        /// <summary>
        ///     Проверяет текущую цену газа на Polygon.
        ///     Relay лимит: 800 gwei. Если текущая цена ниже — можно отправлять.
        /// </summary>
        [ContextMenu(nameof(CheckPolygonGasPrice))]
        public async void CheckPolygonGasPrice()
        {
            const long RELAY_MAX_GAS_PRICE = 800_000_000_000; // 800 gwei

            Debug.Log("=== Checking Polygon Gas Price ===");

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_gasPrice",
                @params = System.Array.Empty<object>(),
            };

            try
            {
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(137, request, destroyCancellationToken);
                string gasPriceHex = response.result?.ToString() ?? "0x0";
                BigInteger gasPriceWei = gasPriceHex.HexToNumber();

                double gasPriceGwei = (double)gasPriceWei / 1_000_000_000;
                double relayLimitGwei = (double)RELAY_MAX_GAS_PRICE / 1_000_000_000;

                Debug.Log($"Current gas price: {gasPriceGwei:F2} gwei");
                Debug.Log($"Relay limit: {relayLimitGwei:F2} gwei");

                if (gasPriceWei <= RELAY_MAX_GAS_PRICE)
                    Debug.Log("✅ GAS IS OK! You can send meta-transactions now.");
                else
                {
                    double overage = (((double)gasPriceWei / RELAY_MAX_GAS_PRICE) - 1) * 100;
                    Debug.LogWarning($"❌ Gas too high ({overage:F1}% over limit). Wait and try again later.");
                }
            }
            catch (System.Exception ex) { Debug.LogError($"Failed to check gas: {ex.Message}"); }
        }

        /// <summary>
        ///     Проверяет владельца NFT токена.
        /// </summary>
        [ContextMenu(nameof(CheckNftOwner))]
        public async void CheckNftOwner()
        {
            var contractAddress = "0x167d6b63511a7b5062d1f7b07722fccbbffb5105";
            var tokenId = "210624583337114373395836055367340864637790190801098222508621978860";

            string walletAddress = await ThirdWebTestHelper.GetActiveWallet()!.GetAddress();
            Debug.Log("=== Checking NFT Owner ===");
            Debug.Log($"My wallet: {walletAddress}");
            Debug.Log($"Contract: {contractAddress}");
            Debug.Log($"Token ID: {tokenId}");

            // ownerOf(uint256) selector = 0x6352211e
            var tokenIdBig = BigInteger.Parse(tokenId);
            string tokenIdHex = tokenIdBig.ToString("x").PadLeft(64, '0');
            string data = "0x6352211e" + tokenIdHex;

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_call",
                @params = new object[]
                {
                    new { to = contractAddress, data },
                    "latest",
                },
            };

            try
            {
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(137, request, destroyCancellationToken);
                string ownerHex = response.result?.ToString() ?? "0x";

                // Decode address from result (last 40 chars)
                if (ownerHex.Length >= 42)
                {
                    string owner = "0x" + ownerHex.Substring(ownerHex.Length - 40);
                    Debug.Log($"NFT Owner: {owner}");

                    if (owner.Equals(walletAddress, System.StringComparison.OrdinalIgnoreCase))
                        Debug.Log("✅ YOU own this NFT - transfer should work");
                    else
                        Debug.LogWarning("❌ Someone else owns this NFT! You cannot transfer it.");
                }
                else { Debug.LogWarning($"Unexpected ownerOf result: {ownerHex}"); }
            }
            catch (System.Exception ex) { Debug.LogError($"Failed to check owner: {ex.Message}"); }
        }

        // Configurable test data - change these in Inspector to test with fresh data
        [Header("Gifting Test Data - Test Case 1 (Decentraland Tutorial Wearables)")]
        [SerializeField] private string testRecipientAddress = "0x3e22ff0ef25fce412f00ba0bf5a794611f77c9a1";
        [SerializeField] private string testContractAddress = "0x167d6b63511a7b5062d1f7b07722fccbbffb5105";
        [SerializeField] private string testTokenId = "210624583337114373395836055367340864637790190801098222508621978860";

        [Header("Gifting Test Data - Test Case 2 (Another Collection)")]
        [SerializeField] private string testRecipientAddress2 = "0xda2d974646fa7ee9f75f288db2050aae09c3ba1f";
        [SerializeField] private string testContractAddress2 = "0x66871d01e15af85ea6c172b7c4821b0f9bb71880";
        [SerializeField] private string testTokenId2 = "674";

        /// <summary>
        ///     Тест gifting-сценария (NFT transferFrom) через relay.
        ///     Использует Web3RequestSource.Internal для отправки через Decentraland RPC relay.
        ///     Это тестирует fix для "insufficient funds for gas" ошибки.
        ///     ВАЖНО: Измените testTokenId в Inspector если нужно протестировать с новыми данными!
        /// </summary>
        [ContextMenu(nameof(TestGiftingViaRelay))]
        public async void TestGiftingViaRelay()
        {
            // Используем данные из Inspector - можно менять для тестов
            string senderAddress = await ThirdWebTestHelper.GetActiveWallet()!.GetAddress();
            string recipientAddress = testRecipientAddress;
            string contractAddress = testContractAddress;
            string tokenId = testTokenId;

            // Используем ManualTxEncoder как в Web3GiftTransferService
            string data = ManualTxEncoder.EncodeTransferFrom(senderAddress, recipientAddress, tokenId);

            Debug.Log("=== Gifting Relay Test ===");
            Debug.Log($"Sender: {senderAddress}");
            Debug.Log($"Recipient: {recipientAddress}");
            Debug.Log($"Contract: {contractAddress}");
            Debug.Log($"Token ID: {tokenId}");
            Debug.Log($"Encoded data: {data}");

            // Формируем запрос как в Web3GiftTransferService (JObject)
            var tx = new JObject
            {
                ["from"] = senderAddress,
                ["to"] = contractAddress,
                ["data"] = data,
            };

            var request = new EthApiRequest
            {
                id = System.Guid.NewGuid().GetHashCode(),
                method = "eth_sendTransaction",
                @params = new object[] { tx },
            };

            Debug.Log($"Request params: {tx}");

            try
            {
                // Gifting использует Polygon Mainnet (137)
                // Используем Web3RequestSource.Internal для отправки через relay
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(
                    137, // Polygon Mainnet
                    request,
                    Web3RequestSource.Internal,
                    destroyCancellationToken);

                string txHash = response.result?.ToString() ?? "null";

                Debug.Log("✅ Gifting relay transaction sent!");
                Debug.Log($"Transaction Hash: {txHash}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Gifting relay failed: {ex.Message}");
                Debug.LogError($"Full exception: {ex}");
            }
        }

        /// <summary>
        ///     Тест gifting-сценария (NFT transferFrom) через relay - Test Case 2.
        ///     Использует другую коллекцию для проверки что EIP-712 domain работает универсально.
        /// </summary>
        [ContextMenu(nameof(TestGiftingViaRelay2))]
        public async void TestGiftingViaRelay2()
        {
            // Используем данные Test Case 2 из Inspector
            string senderAddress = await ThirdWebTestHelper.GetActiveWallet()!.GetAddress();
            string recipientAddress = testRecipientAddress2;
            string contractAddress = testContractAddress2;
            string tokenId = testTokenId2;

            // Используем ManualTxEncoder как в Web3GiftTransferService
            string data = ManualTxEncoder.EncodeTransferFrom(senderAddress, recipientAddress, tokenId);

            Debug.Log("=== Gifting Relay Test (Case 2) ===");
            Debug.Log($"Sender: {senderAddress}");
            Debug.Log($"Recipient: {recipientAddress}");
            Debug.Log($"Contract: {contractAddress}");
            Debug.Log($"Token ID: {tokenId}");
            Debug.Log($"Encoded data: {data}");

            // Формируем запрос как в Web3GiftTransferService (JObject)
            var tx = new JObject
            {
                ["from"] = senderAddress,
                ["to"] = contractAddress,
                ["data"] = data,
            };

            var request = new EthApiRequest
            {
                id = System.Guid.NewGuid().GetHashCode(),
                method = "eth_sendTransaction",
                @params = new object[] { tx },
            };

            Debug.Log($"Request params: {tx}");

            try
            {
                // Gifting использует Polygon Mainnet (137)
                // Используем Web3RequestSource.Internal для отправки через relay
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(
                    137, // Polygon Mainnet
                    request,
                    Web3RequestSource.Internal,
                    destroyCancellationToken);

                string txHash = response.result?.ToString() ?? "null";

                Debug.Log("✅ Gifting relay transaction sent (Case 2)!");
                Debug.Log($"Transaction Hash: {txHash}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Gifting relay failed (Case 2): {ex.Message}");
                Debug.LogError($"Full exception: {ex}");
            }
        }

        /// <summary>
        ///     Тест gifting-сценария БЕЗ relay (обычная транзакция).
        ///     Ожидается ошибка "insufficient funds for gas" если у кошелька нет MATIC.
        /// </summary>
        [ContextMenu(nameof(TestGiftingWithoutRelay))]
        public async void TestGiftingWithoutRelay()
        {
            string senderAddress = await ThirdWebTestHelper.GetActiveWallet()!.GetAddress();
            var recipientAddress = "0x3e22ff0ef25fce412f00ba0bf5a794611f77c9a1";
            var contractAddress = "0x167d6b63511a7b5062d1f7b07722fccbbffb5105";
            var tokenId = "210624583337114373395836055367340864637790190801098222508621978860";

            string data = ManualTxEncoder.EncodeTransferFrom(senderAddress, recipientAddress, tokenId);

            Debug.Log("=== Gifting WITHOUT Relay Test (expected to fail) ===");
            Debug.Log($"Encoded data: {data}");

            var tx = new JObject
            {
                ["from"] = senderAddress,
                ["to"] = contractAddress,
                ["data"] = data,
            };

            var request = new EthApiRequest
            {
                id = System.Guid.NewGuid().GetHashCode(),
                method = "eth_sendTransaction",
                @params = new object[] { tx },
            };

            try
            {
                // Используем Web3RequestSource.SDKScene - НЕ relay, обычная транзакция
                // Ожидается "insufficient funds for gas"
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(
                    137, // Polygon Mainnet
                    request,
                    Web3RequestSource.SDKScene,
                    destroyCancellationToken);

                string txHash = response.result?.ToString() ?? "null";
                Debug.Log($"✅ Transaction sent (unexpected success): {txHash}");
            }
            catch (System.Exception ex) { Debug.LogError($"❌ Expected failure (no gas): {ex.Message}"); }
        }

        // ============================================================================
        // SIGNATURE CACHING TESTS - 3 different wearables to verify signature changes
        // ============================================================================

        [Header("Signature Caching Test Data - Wearable 1")]
        [SerializeField] private string cacheTest1_ContractAddress = "";
        [SerializeField] private string cacheTest1_TokenId = "";
        [SerializeField] private string cacheTest1_Recipient = "0x3e22ff0ef25fce412f00ba0bf5a794611f77c9a1";

        [Header("Signature Caching Test Data - Wearable 2")]
        [SerializeField] private string cacheTest2_ContractAddress = "";
        [SerializeField] private string cacheTest2_TokenId = "";
        [SerializeField] private string cacheTest2_Recipient = "0x3e22ff0ef25fce412f00ba0bf5a794611f77c9a1";

        [Header("Signature Caching Test Data - Wearable 3")]
        [SerializeField] private string cacheTest3_ContractAddress = "";
        [SerializeField] private string cacheTest3_TokenId = "";
        [SerializeField] private string cacheTest3_Recipient = "0x3e22ff0ef25fce412f00ba0bf5a794611f77c9a1";

        /// <summary>
        ///     Test 1 of 3 for signature caching verification.
        ///     Run all 3 tests and compare signatures - they should ALL be DIFFERENT.
        ///     If signatures are same, ThirdWeb SDK is caching.
        /// </summary>
        [ContextMenu("CacheTest 1 - First Wearable")]
        public async void CacheTest1_FirstWearable()
        {
            await RunSignatureCacheTestAsync(1, cacheTest1_ContractAddress, cacheTest1_TokenId, cacheTest1_Recipient);
        }

        /// <summary>
        ///     Test 2 of 3 for signature caching verification.
        /// </summary>
        [ContextMenu("CacheTest 2 - Second Wearable")]
        public async void CacheTest2_SecondWearable()
        {
            await RunSignatureCacheTestAsync(2, cacheTest2_ContractAddress, cacheTest2_TokenId, cacheTest2_Recipient);
        }

        /// <summary>
        ///     Test 3 of 3 for signature caching verification.
        /// </summary>
        [ContextMenu("CacheTest 3 - Third Wearable")]
        public async void CacheTest3_ThirdWearable()
        {
            await RunSignatureCacheTestAsync(3, cacheTest3_ContractAddress, cacheTest3_TokenId, cacheTest3_Recipient);
        }

        /// <summary>
        ///     Runs a signature cache test with the given parameters.
        ///     Logs all relevant data for comparison.
        /// </summary>
        private async System.Threading.Tasks.Task RunSignatureCacheTestAsync(int testNumber, string contractAddress, string tokenId, string recipient)
        {
            Debug.Log("");
            Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
            Debug.Log($"║  SIGNATURE CACHING TEST #{testNumber}                                       ║");
            Debug.Log("╚══════════════════════════════════════════════════════════════════╝");

            if (string.IsNullOrEmpty(contractAddress) || string.IsNullOrEmpty(tokenId))
            {
                Debug.LogError($"[CacheTest{testNumber}] Contract address or token ID is empty! Fill in the Inspector.");
                return;
            }

            string senderAddress = await ThirdWebTestHelper.GetActiveWallet()!.GetAddress();

            Debug.Log($"[CacheTest{testNumber}] ▶ INPUT DATA:");
            Debug.Log($"[CacheTest{testNumber}]   Sender: {senderAddress}");
            Debug.Log($"[CacheTest{testNumber}]   Recipient: {recipient}");
            Debug.Log($"[CacheTest{testNumber}]   Contract: {contractAddress}");
            Debug.Log($"[CacheTest{testNumber}]   Token ID: {tokenId}");
            Debug.Log($"[CacheTest{testNumber}]   Timestamp: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC");

            // Encode transferFrom
            string functionSignature = ManualTxEncoder.EncodeTransferFrom(senderAddress, recipient, tokenId);
            Debug.Log($"[CacheTest{testNumber}]   Function Signature: {functionSignature}");

            var tx = new JObject
            {
                ["from"] = senderAddress,
                ["to"] = contractAddress,
                ["data"] = functionSignature,
            };

            var request = new EthApiRequest
            {
                id = System.Guid.NewGuid().GetHashCode(),
                method = "eth_sendTransaction",
                @params = new object[] { tx },
            };

            Debug.Log($"[CacheTest{testNumber}] ▶ SENDING via meta-transaction relay...");

            try
            {
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(
                    137, // Polygon Mainnet
                    request,
                    Web3RequestSource.Internal,
                    destroyCancellationToken);

                string result = response.result?.ToString() ?? "null";

                Debug.Log($"[CacheTest{testNumber}] ▶ RESULT:");
                Debug.Log($"[CacheTest{testNumber}]   ✅ SUCCESS! TxHash: {result}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CacheTest{testNumber}] ▶ RESULT:");
                Debug.LogError($"[CacheTest{testNumber}]   ❌ FAILED: {ex.Message}");

                // Still log the signature from logs if available
                Debug.Log($"[CacheTest{testNumber}]   Check [ThirdWeb] logs above for signature details.");
            }

            Debug.Log($"[CacheTest{testNumber}] ══════════════════════════════════════════════════════════════════");
            Debug.Log("");
        }

        /// <summary>
        ///     Проверяет domain separator для MANA контракта на Amoy.
        ///     Это нужно для отладки donations meta-tx.
        /// </summary>
        [ContextMenu(nameof(CheckManaAmoyDomainSeparator))]
        public async void CheckManaAmoyDomainSeparator()
        {
            var contractAddress = "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0"; // MANA on Amoy
            const int chainId = 80002; // Amoy Testnet

            Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
            Debug.Log("║  MANA AMOY DOMAIN SEPARATOR TEST                                  ║");
            Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
            Debug.Log($"Contract: {contractAddress}");
            Debug.Log($"Chain ID: {chainId} (Amoy)");

            // Get contract's domain separator - try both function names
            var contractDomainSep = "";

            // Try domainSeparator() first (0xf698da25)
            var request1 = new EthApiRequest
            {
                id = 1,
                method = "eth_call",
                @params = new object[]
                {
                    new { to = contractAddress, data = "0xf698da25" }, // domainSeparator()
                    "latest",
                },
            };

            try
            {
                EthApiResponse response = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(chainId, request1, destroyCancellationToken);
                contractDomainSep = response.result?.ToString() ?? "0x";
                Debug.Log($"domainSeparator() returned: {contractDomainSep}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"domainSeparator() failed: {ex.Message}");

                // Try getDomainSeparator() (0xed24911d)
                var request2 = new EthApiRequest
                {
                    id = 1,
                    method = "eth_call",
                    @params = new object[]
                    {
                        new { to = contractAddress, data = "0xed24911d" }, // getDomainSeparator()
                        "latest",
                    },
                };

                try
                {
                    EthApiResponse response2 = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(chainId, request2, destroyCancellationToken);
                    contractDomainSep = response2.result?.ToString() ?? "0x";
                    Debug.Log($"getDomainSeparator() returned: {contractDomainSep}");
                }
                catch (System.Exception ex2) { Debug.LogWarning($"getDomainSeparator() also failed: {ex2.Message}"); }
            }

            if (string.IsNullOrEmpty(contractDomainSep) || contractDomainSep == "0x")
            {
                Debug.LogError("❌ Could not get domain separator from contract!");
                return;
            }

            Debug.Log("");
            Debug.Log($"★★★ CONTRACT DOMAIN SEPARATOR: {contractDomainSep}");
            Debug.Log("");

            // Test different name/version combinations
            Debug.Log("Testing different EIP-712 domain configurations...");
            Debug.Log("");

            var found = false;
            string[] names = { "Decentraland MANA", "MANA", "Decentraland", "(PoS) Decentraland MANA", "Dummy ERC20" };
            string[] versions = { "1", "2" };

            foreach (string name in names)
            {
                foreach (string version in versions)
                {
                    string sep = ThirdWebAuthenticator.ComputeDomainSeparator(name, version, contractAddress, chainId);
                    bool match = contractDomainSep.Equals(sep, System.StringComparison.OrdinalIgnoreCase);

                    if (match)
                    {
                        Debug.Log($"✅ MATCH! name='{name}', version='{version}'");
                        Debug.Log($"   {sep}");
                        found = true;
                    }
                }
            }

            if (!found)
            {
                Debug.Log("No match with common names. Testing all computed separators:");
                Debug.Log("");

                foreach (string name in names)
                {
                    foreach (string version in versions)
                    {
                        string sep = ThirdWebAuthenticator.ComputeDomainSeparator(name, version, contractAddress, chainId);
                        Debug.Log($"['{name}', v='{version}'] {sep}");
                    }
                }

                Debug.Log("");
                Debug.LogError("❌ NO MATCH FOUND! Need to find correct EIP-712 parameters.");
                Debug.Log("Try checking contract source on Amoy explorer.");
            }

            // Also get contract name
            Debug.Log("");
            Debug.Log("Getting contract name()...");

            try
            {
                var nameRequest = new EthApiRequest
                {
                    id = 1,
                    method = "eth_call",
                    @params = new object[]
                    {
                        new { to = contractAddress, data = "0x06fdde03" }, // name()
                        "latest",
                    },
                };

                EthApiResponse nameResponse = await ThirdWebTestHelper.GetAuthenticator()!.SendAsync(chainId, nameRequest, destroyCancellationToken);
                string nameHex = nameResponse.result?.ToString() ?? "0x";

                if (nameHex.Length > 130)
                {
                    string clean = nameHex.StartsWith("0x") ? nameHex.Substring(2) : nameHex;
                    var length = System.Convert.ToInt32(clean.Substring(64, 64), 16);
                    string dataHex = clean.Substring(128, length * 2);
                    var bytes = new byte[dataHex.Length / 2];

                    for (var i = 0; i < bytes.Length; i++)
                        bytes[i] = System.Convert.ToByte(dataHex.Substring(i * 2, 2), 16);

                    string? contractName = System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    Debug.Log($"Contract name(): '{contractName}'");

                    // Test with actual contract name
                    foreach (string version in versions)
                    {
                        string sep = ThirdWebAuthenticator.ComputeDomainSeparator(contractName, version, contractAddress, chainId);
                        bool match = contractDomainSep.Equals(sep, System.StringComparison.OrdinalIgnoreCase);

                        if (match)
                        {
                            Debug.Log($"✅ MATCH with contract name! name='{contractName}', version='{version}'");
                            found = true;
                        }
                        else { Debug.Log($"['{contractName}', v='{version}'] {sep}"); }
                    }
                }
            }
            catch (System.Exception ex) { Debug.LogWarning($"Failed to get contract name: {ex.Message}"); }

            Debug.Log("══════════════════════════════════════════════════════════════════");
        }

        /// <summary>
        ///     Prints comparison summary after running all 3 cache tests.
        ///     Call this after running CacheTest 1, 2, and 3 to see if signatures differ.
        /// </summary>
        [ContextMenu("CacheTest - Print Summary")]
        public void CacheTestPrintSummary()
        {
            Debug.Log("");
            Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
            Debug.Log("║  SIGNATURE CACHING TEST SUMMARY                                  ║");
            Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
            Debug.Log("");
            Debug.Log("To verify if ThirdWeb SDK is caching signatures:");
            Debug.Log("");
            Debug.Log("1. Search logs for '[ThirdWeb] Full signature:' from each test");
            Debug.Log("2. Compare the 3 signatures:");
            Debug.Log("");
            Debug.Log("   ✅ If ALL 3 signatures are DIFFERENT → No caching, working correctly");
            Debug.Log("   ❌ If ANY 2 signatures are SAME → SDK is caching signatures!");
            Debug.Log("");
            Debug.Log("Also compare '[EIP712-Hash] ★ Final digest:' values:");
            Debug.Log("   - Different digests = different typed data (expected)");
            Debug.Log("   - Same digests = something wrong with our hash computation");
            Debug.Log("");
            Debug.Log("══════════════════════════════════════════════════════════════════");
        }
    }
}
