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
        // private readonly string email = "popuzin@gmail.com";
        // private readonly string password = "secret123";

        private readonly string loginUrl = "https://poc-login-server.vercel.app/api/login";
        private readonly string registerUrl = "https://poc-login-server.vercel.app/api/register";
        private readonly string checkUrl = "https://poc-login-server.vercel.app/api/check-confirmed";
        private readonly int chainId = 1; // Ethereum mainnet

        public async Task Login(string email, string password)
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

            await ConnectWallet(jwt);
        }

        public async Task Register(string email, string password)
        {
            Debug.Log("📮 Starting registration...");
            bool registered = await RegisterUser(email, password);

            if (registered) { Debug.Log("✅ Registration request sent. Please confirm via email."); }
            else { Debug.LogWarning("⚠️ Registration failed. See errors above."); }
        }

        public async Task CheckConfirmed(string email)
        {
            Debug.Log("🔎 Checking confirmation status...");
            bool confirmed = await IsConfirmed(email);

            if (confirmed) { Debug.Log("✅ Account is confirmed."); }
            else { Debug.LogWarning("⏳ Account is NOT confirmed yet."); }
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

            if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text)) { Debug.LogError($"↩ Login body: {request.downloadHandler.text}"); }
            return null;
        }

        private async Task<bool> RegisterUser(string userEmail, string userPassword)
        {
            var jsonData = $"{{\"email\":\"{userEmail}\",\"password\":\"{userPassword}\"}}";

            using var request = new UnityWebRequest(registerUrl, "POST");

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequestAsync();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                Debug.Log($"✓ Register response: {response}");

                try
                {
                    RegisterResponse data = JsonUtility.FromJson<RegisterResponse>(response);
                    return !string.IsNullOrEmpty(data?.message);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ Failed to parse register response: {ex.Message}");
                    return false;
                }
            }

            Debug.LogError($"❌ Register error: {request.error} (HTTP {request.responseCode})");

            if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text)) { Debug.LogError($"↩ Register body: {request.downloadHandler.text}"); }

            return false;
        }

        private async Task<bool> IsConfirmed(string userEmail)
        {
            var url = $"{checkUrl}?email={UnityWebRequest.EscapeURL(userEmail)}";
            using var request = UnityWebRequest.Get(url);

            await request.SendWebRequestAsync();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                Debug.Log($"✓ Check-confirmed response: {response}");

                try
                {
                    CheckConfirmedResponse data = JsonUtility.FromJson<CheckConfirmedResponse>(response);
                    return data != null && data.confirmed;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ Failed to parse check-confirmed response: {ex.Message}");
                    return false;
                }
            }

            Debug.LogError($"❌ Check-confirmed error: {request.error} (HTTP {request.responseCode})");

            if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text)) { Debug.LogError($"↩ Check-confirmed body: {request.downloadHandler.text}"); }

            return false;
        }

        private async Task ConnectWallet(string jwt)
        {
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

        [Serializable]
        private class RegisterResponse
        {
            public string message;
            public string email;
        }

        [Serializable]
        private class CheckConfirmedResponse
        {
            public string email;
            public bool confirmed;
        }
    }
}
