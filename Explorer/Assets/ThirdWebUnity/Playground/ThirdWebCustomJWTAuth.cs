using System;
using System.Text;
using System.Threading.Tasks;
using Thirdweb;
using UnityEngine;
using UnityEngine.Networking;

namespace ThirdWebUnity.Playground
{
    public class ThirdWebCustomJWTAuth
    {
        private readonly string email = "popuzin@gmail.com";
        private readonly string loginUrl = "https://poc-login-server.vercel.app/api/login";
        private readonly string password = "secret123";
        private readonly int chainId = 1; // Ethereum mainnet

        public async Task Login()
        {
            Debug.Log("🚀 Starting thirdweb JWT auth test...");

            string jwt; // = jwtToken;

            // if (string.IsNullOrWhiteSpace(jwt))
            {
                Debug.Log($"📤 Fetching JWT from: {loginUrl}");
                jwt = await LoginAndGetJwt(email, password);
            }

            if (string.IsNullOrWhiteSpace(jwt))
            {
                Debug.LogError("❌ No JWT available. Provide jwtToken in Inspector or ensure login succeeds.");
                return;
            }

            Debug.Log("🔑 JWT acquired , connecting to thirdweb...");

            try
            {
                var inAppWalletOptions = new ThirdWebManager.InAppWalletOptions(
                    authprovider: AuthProvider.JWT,
                    jwtOrPayload: jwt
                );

                var options = new ThirdWebManager.WalletOptions(
                    ThirdWebManager.WalletProvider.InAppWallet,
                    chainId,
                    inAppWalletOptions
                );

                IThirdwebWallet wallet = await ThirdWebManager.Instance.ConnectWallet(options);
                string address = await wallet.GetAddress();
                Debug.Log($"🎉 SUCCESS! Wallet connected: {address}");
            }
            catch (Exception e) { Debug.LogError($"❌ thirdweb connection failed: {e.Message}"); }
        }

        private async Task<string> LoginAndGetJwt(string userEmail, string userPassword)
        {
            var jsonData = $"{{\"email\":\"{userEmail}\",\"password\":\"{userPassword}\"}}";

            using var request = new UnityWebRequest(loginUrl, "POST");

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequestAsync();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                Debug.Log($"✓ Login response: {response}");

                try
                {
                    LoginResponse data = JsonUtility.FromJson<LoginResponse>(response);
                    return data?.token;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ Failed to parse login response: {ex.Message}");
                    return null;
                }
            }

            Debug.LogError($"❌ Login error: {request.error} (HTTP {request.responseCode})");
            return null;
        }

        [Serializable]
        private class LoginResponse
        {
            public string message;
            public string token;
            public User user;
        }

        [Serializable]
        private class User
        {
            public string email;
            public bool confirmed;
        }
    }
}
