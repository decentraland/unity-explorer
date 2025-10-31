using System;
using System.Text;
using System.Threading.Tasks;
using Thirdweb;
using Thirdweb.Unity;
using UnityEngine;
using UnityEngine.Networking;

public class CustomAuthTest : MonoBehaviour
{
    // URL вашего локального сервера (через localtunnel)
    [SerializeField]
    private string loginUrl = "https://five-masks-teach.loca.lt/auth/login";

    private async void Start() =>
        await TestCustomAuth();

    public async Task TestCustomAuth()
    {
        Debug.Log("🚀 Starting custom auth test...");

        // Шаг 1: Логин на ваш backend
        string payload = await LoginToBackend("alice", "secret123");

        if (string.IsNullOrEmpty(payload))
        {
            Debug.LogError("❌ Login failed!");
            return;
        }

        Debug.Log($"✓ Got payload: {payload}");

        // Шаг 2: Подключаемся через ThirdWeb
        await ConnectWithThirdweb(payload);
    }

    private async Task<string> LoginToBackend(string username, string password)
    {
        Debug.Log($"📤 Calling login endpoint: {loginUrl}");

        string jsonData = $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}";

        using (var request = new UnityWebRequest(loginUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequestAsync();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                Debug.Log($"✓ Login response: {response}");
                LoginResponse data = JsonUtility.FromJson<LoginResponse>(response);
                return data != null ? data.payload : null;
            }

            Debug.LogError($"❌ Login error: {request.error} (HTTP {request.responseCode})");
            return null;
        }
    }

    private async Task ConnectWithThirdweb(string payload)
    {
        Debug.Log("📤 Connecting to ThirdWeb with payload...");

        try
        {
            var inAppWalletOptions = new InAppWalletOptions(
                authprovider: AuthProvider.AuthEndpoint,
                jwtOrPayload: payload
            );

            var options = new WalletOptions(
                WalletProvider.InAppWallet,
                1,
                inAppWalletOptions
            );

            IThirdwebWallet wallet = await ThirdwebManager.Instance.ConnectWallet(options);

            string address = await wallet.GetAddress();
            Debug.Log($"🎉 SUCCESS! Wallet connected: {address}");
        }
        catch (Exception e) { Debug.LogError($"❌ ThirdWeb connection failed: {e.Message}"); }
    }

    [Serializable]
    private class LoginResponse
    {
        public string payload;
    }
}

public static class UnityWebRequestExtensions
{
    public static Task SendWebRequestAsync(this UnityWebRequest request)
    {
        var tcs = new TaskCompletionSource<bool>();
        UnityWebRequestAsyncOperation op = request.SendWebRequest();
        op.completed += _ => tcs.SetResult(true);
        return tcs.Task;
    }
}
