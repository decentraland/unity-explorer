using DCL.Web3.Abstract;
using Thirdweb;
using ThirdWebUnity;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public class TestOnChainWeb3 : MonoBehaviour
    {
        [ContextMenu(nameof(TestGetBalance))]
        public async void TestGetBalance()
        {
            string walletAddress = await ThirdWebManager.Instance.ActiveWallet.GetAddress();

            var request = new EthApiRequest
            {
                id = 1,
                method = "eth_getBalance",
                @params = new object[] { walletAddress, "latest" },
            };

            EthApiResponse response = await ThirdWebAuthenticator.Instance.SendAsync(request, destroyCancellationToken);

            var hexBalance = response.result.ToString();
            Debug.Log($"Balance (ETH): {hexBalance.HexToNumber()}");
            Debug.Log($"Balance (ETH): {hexBalance.HexToString()}");

            // Debug.Log($"Balance (ETH): {hexBalance.ToWei()}");
        }
    }
}
