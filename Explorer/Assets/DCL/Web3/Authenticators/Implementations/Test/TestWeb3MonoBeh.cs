using ThirdWebUnity;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public class TestWeb3MonoBeh : MonoBehaviour
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
            Debug.Log($"net_version result: {response.result}");
        }
    }
}
