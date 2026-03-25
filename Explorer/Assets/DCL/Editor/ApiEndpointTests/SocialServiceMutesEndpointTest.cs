using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DCL.Editor.ApiEndpointTests
{
    /// <summary>
    /// Health-check for Social Service User Mutes API.
    /// No authentication required — tests endpoint availability only.
    /// 401 = endpoint exists (auth required), 404 = not deployed, connection error = server down.
    /// </summary>
    public static class SocialServiceMutesEndpointTest
    {
        private const string ZONE_URL = "https://social-api.decentraland.zone/v1/mutes";
        private const string ORG_URL = "https://social-api.decentraland.org/v1/mutes";
        private const string MENU = "Decentraland/API Endpoint Tests/Social Service/User Mutes/";
        private const string DUMMY_BODY = "{\"muted_address\": \"0x0000000000000000000000000000000000000000\"}";

        [MenuItem(MENU + "GET (zone)")]
        private static void GetZone() => Send(ZONE_URL, "GET");

        [MenuItem(MENU + "GET (org)")]
        private static void GetOrg() => Send(ORG_URL, "GET");

        [MenuItem(MENU + "POST (zone)")]
        private static void PostZone() => Send(ZONE_URL, "POST", DUMMY_BODY);

        [MenuItem(MENU + "POST (org)")]
        private static void PostOrg() => Send(ORG_URL, "POST", DUMMY_BODY);

        [MenuItem(MENU + "DELETE (zone)")]
        private static void DeleteZone() => Send(ZONE_URL, "DELETE", DUMMY_BODY);

        [MenuItem(MENU + "DELETE (org)")]
        private static void DeleteOrg() => Send(ORG_URL, "DELETE", DUMMY_BODY);

        private static void Send(string url, string method, string jsonBody = null)
        {
            Debug.Log($"[API Test] {method} {url} — sending...");

            var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (jsonBody != null)
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            void PollResult()
            {
                if (!operation.isDone) return;

                EditorApplication.update -= PollResult;
                LogResult(method, url, request);
                request.Dispose();
            }

            EditorApplication.update += PollResult;
        }

        private static void LogResult(string method, string url, UnityWebRequest request)
        {
            string responseBody = request.downloadHandler?.text ?? "";
            long code = request.responseCode;

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError($"[API Test] {method} {url}\n  FAIL — Connection error: {request.error}\n  Backend is DOWN or unreachable");
                return;
            }

            if (code == 404)
            {
                Debug.LogError($"[API Test] {method} {url}\n  FAIL — 404 Not Found: endpoint NOT deployed\n  {responseBody}");
                return;
            }

            if (code >= 200 && code < 500)
                Debug.Log($"[API Test] {method} {url}\n  OK — {code}: backend is alive\n  {responseBody}");
            else
                Debug.LogWarning($"[API Test] {method} {url}\n  WARN — {code}: unexpected status\n  {responseBody}");
        }
    }
}
