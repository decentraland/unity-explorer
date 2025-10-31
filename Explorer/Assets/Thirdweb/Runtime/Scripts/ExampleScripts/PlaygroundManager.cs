using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Thirdweb.Unity.Examples
{
    public enum OAuthProvider
    {
        Google,
        Apple,
        Facebook,
        Discord,
        Twitch,
        Github,
        Coinbase,
        X,
        TikTok,
        Line,
        Steam,
    }

    /// <summary>
    ///     A simple manager to demonstrate core functionality of the thirdweb SDK.
    ///     This is not production-ready code. Do not use this in production.
    /// </summary>
    public class PlaygroundManager : MonoBehaviour
    {
#region Inspector
        public bool AlwaysUpgradeToSmartWallet;
        public ulong ChainId;
        public string Email;
        public string Phone;
        public OAuthProvider Social = OAuthProvider.Google;
        public Transform ActionButtonParent;
        public GameObject ActionButtonPrefab;
        public GameObject LogPanel;
#endregion

#region Initialization
        private Dictionary<string, UnityAction> _actionMappings;

        private void Awake()
        {
            LogPanel.SetActive(false);

            _actionMappings = new Dictionary<string, UnityAction>
            {
                // Wallet Connection
                { "Guest Wallet (Smart)", Wallet_Guest },
                { "Social Wallet", Wallet_Social },
                { "Email Wallet", Wallet_Email },
                { "Phone Wallet", Wallet_Phone },
                { "External Wallet", Wallet_External },

                // Wallet Actions
                { "Sign Message", WalletAction_SignMessage },
                { "Sign SIWE", WalletAction_SIWE },
                { "Sign Typed Data", WalletAction_SignTypedData },
                { "Get Balance", WalletAction_GetBalance },
                { "Send Assets", WalletAction_Transfer },

                // Contract Interaction
                { "Read Contract (Ext)", Contract_Read },
                { "Write Contract (Ext)", Contract_Write },
                { "Read Contract (Raw)", Contract_ReadCustom },
                { "Write Contract (Raw)", Contract_WriteCustom },
                { "Prepare Tx (Low Lvl)", Contract_PrepareTransaction },
            };

            foreach (Transform child in ActionButtonParent)
                Destroy(child.gameObject);

            foreach (KeyValuePair<string, UnityAction> action in _actionMappings)
            {
                GameObject button = Instantiate(ActionButtonPrefab, ActionButtonParent);
                button.GetComponentInChildren<TMP_Text>().text = action.Key;
                button.GetComponent<Button>().onClick.AddListener(action.Value);
            }
        }

        private void LogPlayground(string message)
        {
            LogPanel.GetComponentInChildren<TMP_Text>().text = message;
            ThirdwebDebug.Log(message);
            LogPanel.SetActive(true);
        }

        private bool WalletConnected()
        {
            bool isConnected = ThirdwebManager.Instance.ActiveWallet != null;

            if (!isConnected)
                LogPlayground("Please authenticate to connect a wallet first.");

            return isConnected;
        }
#endregion

#region Wallet Connection
        private async void Wallet_Guest()
        {
            var walletOptions = new WalletOptions(WalletProvider.InAppWallet, ChainId, new InAppWalletOptions(authprovider: AuthProvider.Guest));
            IThirdwebWallet wallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);

            string address = await wallet.GetAddress();
            ThirdwebDebug.Log($"[Guest] Smart wallet admin signer address:\n{address}");

            // --For the guest wallet to showcase ERC4337 we're gonna upgrade it to a smart wallet
            SmartWallet smartWallet = await ThirdwebManager.Instance.UpgradeToSmartWallet(
                ThirdwebManager.Instance.ActiveWallet,
                ChainId,
                new SmartWalletOptions(

                    // sponsor gas for users, improve onboarding
                    true,

                    // optional
                    Constants.DEFAULT_FACTORY_ADDRESS_V07,

                    // optional
                    entryPoint: Constants.ENTRYPOINT_ADDRESS_V07
                )
            );

            string smartWalletAddress = await smartWallet.GetAddress();
            LogPlayground($"[Guest] Connected to wallet (sponsored gas):\n{smartWalletAddress}");

            // // --Smart wallets have special functionality other than just gas sponsorship
            // var sessionKeyReceipt = await smartWallet.CreateSessionKey(
            //     signerAddress: await Utils.GetAddressFromENS(ThirdwebManager.Instance.Client, "vitalik.eth"),
            //     approvedTargets: new List<string> { Constants.ADDRESS_ZERO },
            //     nativeTokenLimitPerTransactionInWei: "0",
            //     permissionStartTimestamp: "0",
            //     permissionEndTimestamp: (Utils.GetUnixTimeStampNow() + (60 * 60)).ToString(), // 1 hour from now
            //     reqValidityStartTimestamp: "0",
            //     reqValidityEndTimestamp: (Utils.GetUnixTimeStampNow() + (60 * 60)).ToString() // 1 hour from now
            // );

            // this.LogPlayground($"Session Key Creation Receipt:\n{sessionKeyReceipt}");
        }

        private async void Wallet_Social()
        {
            if (!Enum.TryParse(Social.ToString(), out AuthProvider parsedOAuthProvider))
                parsedOAuthProvider = AuthProvider.Google;

            var walletOptions = new WalletOptions(WalletProvider.InAppWallet, ChainId, new InAppWalletOptions(authprovider: parsedOAuthProvider));
            IThirdwebWallet wallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);

            if (AlwaysUpgradeToSmartWallet)
                wallet = await ThirdwebManager.Instance.UpgradeToSmartWallet(wallet, ChainId, new SmartWalletOptions(true));

            string address = await wallet.GetAddress();
            LogPlayground($"[Social] Connected to wallet:\n{address}");
        }

        private async void Wallet_Email()
        {
            if (string.IsNullOrEmpty(Email))
            {
                LogPlayground("Please enter a valid email address in the scene's PlaygroundManager.");
                return;
            }

            var walletOptions = new WalletOptions(WalletProvider.InAppWallet, ChainId, new InAppWalletOptions(Email));
            IThirdwebWallet wallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);

            if (AlwaysUpgradeToSmartWallet)
                wallet = await ThirdwebManager.Instance.UpgradeToSmartWallet(wallet, ChainId, new SmartWalletOptions(true));

            string address = await wallet.GetAddress();
            LogPlayground($"[Email] Connected to wallet:\n{address}");
        }

        private async void Wallet_Phone()
        {
            if (string.IsNullOrEmpty(Phone))
            {
                LogPlayground("Please enter a valid phone number in the scene's PlaygroundManager.");
                return;
            }

            var walletOptions = new WalletOptions(WalletProvider.InAppWallet, ChainId, new InAppWalletOptions(phoneNumber: Phone));
            IThirdwebWallet wallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);

            if (AlwaysUpgradeToSmartWallet)
                wallet = await ThirdwebManager.Instance.UpgradeToSmartWallet(wallet, ChainId, new SmartWalletOptions(true));

            string address = await wallet.GetAddress();
            LogPlayground($"[Phone] Connected to wallet:\n{address}");
        }

        private async void Wallet_External()
        {
            var walletOptions = new WalletOptions(
                WalletProvider.ReownWallet,
                ChainId,
                reownOptions: new ReownOptions(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    new[]
                    {
                        "c57ca95b47569778a828d19178114f4db188b89b763c899ba0be274e97267d96",
                        "18388be9ac2d02726dbac9777c96efaac06d744b2f6d580fccdd4127a6d01fd1",
                        "541d5dcd4ede02f3afaf75bf8e3e4c4f1fb09edb5fa6c4377ebf31c2785d9adf",
                    }
                )
            );

            IThirdwebWallet wallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);

            if (AlwaysUpgradeToSmartWallet)
                wallet = await ThirdwebManager.Instance.UpgradeToSmartWallet(wallet, ChainId, new SmartWalletOptions(true));

            string address = await wallet.GetAddress();
            LogPlayground($"[SIWE] Connected to wallet:\n{address}");
        }

#pragma warning disable IDE0051 // Remove unused private members: This is a showcase of an alternative way to use Reown
        private async void Wallet_External_Direct()
#pragma warning restore IDE0051 // Remove unused private members: This is a showcase of an alternative way to use Reown
        {
            var walletOptions = new WalletOptions(
                WalletProvider.ReownWallet,
                ChainId,
                reownOptions: new ReownOptions(singleWalletId: "c57ca95b47569778a828d19178114f4db188b89b763c899ba0be274e97267d96")
            );

            IThirdwebWallet wallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);

            if (AlwaysUpgradeToSmartWallet)
                wallet = await ThirdwebManager.Instance.UpgradeToSmartWallet(wallet, ChainId, new SmartWalletOptions(true));

            string address = await wallet.GetAddress();
            LogPlayground($"[SIWE] Connected to wallet:\n{address}");
        }
#endregion

#region Wallet Actions
        private async void WalletAction_SignMessage()
        {
            if (!WalletConnected())
                return;

            string message = "Hello from thirdweb!";
            string sig = await ThirdwebManager.Instance.ActiveWallet.PersonalSign(message);
            LogPlayground($"Message:\n{message}\n\nSignature:\n{sig}");
        }

        private async void WalletAction_SIWE()
        {
            if (!WalletConnected())
                return;

            string payload = Utils.GenerateSIWE(
                new LoginPayloadData
                {
                    Domain = "thirdweb.com",
                    Address = await ThirdwebManager.Instance.ActiveWallet.GetAddress(),
                    Statement = "Sign in with thirdweb to the Unity SDK playground.",
                    Version = "1",
                    ChainId = ChainId.ToString(),
                    Nonce = Guid.NewGuid().ToString(),
                    IssuedAt = DateTime.UtcNow.ToString("o"),
                }
            );

            string sig = await ThirdwebManager.Instance.ActiveWallet.PersonalSign(payload);
            LogPlayground($"SIWE Payload:\n{payload}\n\nSignature:\n{sig}");
        }

        private async void WalletAction_SignTypedData()
        {
            if (!WalletConnected())
                return;

            string json =
                "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"},{\"name\":\"version\",\"type\":\"string\"},{\"name\":\"chainId\",\"type\":\"uint256\"},{\"name\":\"verifyingContract\",\"type\":\"address\"}],\"Person\":[{\"name\":\"name\",\"type\":\"string\"},{\"name\":\"wallet\",\"type\":\"address\"}],\"Mail\":[{\"name\":\"from\",\"type\":\"Person\"},{\"name\":\"to\",\"type\":\"Person\"},{\"name\":\"contents\",\"type\":\"string\"}]},\"primaryType\":\"Mail\",\"domain\":{\"name\":\"Ether Mail\",\"version\":\"1\",\"chainId\":1,\"verifyingContract\":\"0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC\"},\"message\":{\"from\":{\"name\":\"Cow\",\"wallet\":\"0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826\"},\"to\":{\"name\":\"Bob\",\"wallet\":\"0xbBbBBBBbbBBBbbbBbbBbbBBbBbbBbBbBbBbbBBbB\"},\"contents\":\"Hello, Bob!\"}}";

            string sig = await ThirdwebManager.Instance.ActiveWallet.SignTypedDataV4(json);
            LogPlayground($"Typed Data:\n{json}\n\nSignature:\n{sig}");
        }

        private async void WalletAction_GetBalance()
        {
            if (!WalletConnected())
                return;

            BigInteger balance = await ThirdwebManager.Instance.ActiveWallet.GetBalance(ChainId);
            ThirdwebChainData chainMeta = await Utils.GetChainMetadata(ThirdwebManager.Instance.Client, ChainId);
            LogPlayground($"Wallet Balance:\n{balance.ToString().ToEth(6, true)} {chainMeta.NativeCurrency.Symbol}");
        }

        private async void WalletAction_Transfer()
        {
            if (!WalletConnected())
                return;

            // ---Transfer native tokens
            ThirdwebTransactionReceipt receipt = await ThirdwebManager.Instance.ActiveWallet.Transfer(ChainId, await ThirdwebManager.Instance.ActiveWallet.GetAddress(), 0);

            // ---Transfer ERC20 tokens
            // var receipt = await ThirdwebManager.Instance.ActiveWallet.Transfer(chainId: this.ChainId, toAddress: await ThirdwebManager.Instance.ActiveWallet.GetAddress(), weiAmount: 0, tokenAddress: "0xERC20Addy");

            LogPlayground($"Transfer Receipt:\n{receipt}");
        }
#endregion

#region Contract Interaction
        private async void Contract_Read()
        {
            string usdcContractAddressArbitrum = "0xaf88d065e77c8cc2239327c5edb3a432268e5831";
            ThirdwebContract contract = await ThirdwebManager.Instance.GetContract(usdcContractAddressArbitrum, 42161);
            BigInteger result = await contract.ERC20_BalanceOf(await Utils.GetAddressFromENS(ThirdwebManager.Instance.Client, "vitalik.eth"));
            string tokenSymbol = await contract.ERC20_Symbol();
            string resultFormatted = result.ToString().ToEth(6, true) + " " + tokenSymbol;
            LogPlayground($"ThirdwebContract.ERC20_BalanceOf Result:\n{resultFormatted}");
        }

        private async void Contract_Write()
        {
            if (!WalletConnected())
                return;

            ThirdwebContract contract = await ThirdwebManager.Instance.GetContract("0x3EE304A2cBf24F73510C6C590cFcd10bEd0E6F70", ChainId);

            ThirdwebTransactionReceipt transactionReceipt = await contract.ERC20_Approve(
                ThirdwebManager.Instance.ActiveWallet,
                await Utils.GetAddressFromENS(ThirdwebManager.Instance.Client, "vitalik.eth"),
                0
            );

            LogPlayground($"ThirdwebContract.ERC20_Approve Receipt:\n{transactionReceipt}");
        }

        private async void Contract_ReadCustom()
        {
            ThirdwebContract contract = await ThirdwebManager.Instance.GetContract("0xBD0334AC7FADA28CcD27Fa09838e9EA4c39117Db", ChainId);
            string method = "function getDeposit() view returns (uint256)";
            BigInteger result = await contract.Read<BigInteger>(method);
            LogPlayground($"ThirdwebContract.Read<T>\n({method}\nResult:\n{result}");
        }

        private async void Contract_WriteCustom()
        {
            if (!WalletConnected())
                return;

            ThirdwebContract contract = await ThirdwebManager.Instance.GetContract("0xBD0334AC7FADA28CcD27Fa09838e9EA4c39117Db", ChainId);
            string method = "function transferOwnership(address newOwner) payable";

            // just in case someday this actually goes through for some unknown reason, we can count on vitalik
            string newOwner = await Utils.GetAddressFromENS(ThirdwebManager.Instance.Client, "vitalik.eth");
            ThirdwebTransactionReceipt transactionReceipt = await contract.Write(ThirdwebManager.Instance.ActiveWallet, method, 0, newOwner);
            LogPlayground($"ThirdwebContract.Write\n({method}\nReceipt:\n{transactionReceipt}");
        }

        private async void Contract_PrepareTransaction()
        {
            if (!WalletConnected())
                return;

            // ---You can prepare a transaction instead of directly calling Thirdweb.Contract.Write
            // var contract = await ThirdwebManager.Instance.GetContract(address: "0xBD0334AC7FADA28CcD27Fa09838e9EA4c39117Db", chainId: this.ChainId);
            // var method = "function transferOwnership(address newOwner) payable";
            // var newOwner = await Utils.GetAddressFromENS(ThirdwebManager.Instance.Client, "vitalik.eth");
            // var transaction = await contract.Prepare(wallet: ThirdwebManager.Instance.ActiveWallet, method: method, weiValue: 0, parameters: new object[] { newOwner });

            // ---Or you can create a transaction from scratch
            ThirdwebTransaction transaction = await ThirdwebTransaction.Create(
                ThirdwebManager.Instance.ActiveWallet,
                new ThirdwebTransactionInput(ChainId, to: await ThirdwebManager.Instance.ActiveWallet.GetAddress(), value: 0, data: "0x")
            );

            TotalCosts costEstimates = await ThirdwebTransaction.EstimateTotalCosts(transaction);

            // ---If you wanted to send it
            // `var hash = transaction.Send();` or `var receipt = await transaction.SendAndWaitForTransactionReceipt();`

            // ---We're just gonna log it here
            LogPlayground($"ThirdwebContract.Prepare\nEstimated Cost:\n{costEstimates.Ether}\n\nTransaction:\n{JsonConvert.SerializeObject(transaction, Formatting.Indented)}");
        }
#endregion
    }
}
