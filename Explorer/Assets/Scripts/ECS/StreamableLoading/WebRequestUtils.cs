using ECS.StreamableLoading.Common.Components;
using UnityEngine.Networking;

namespace ECS.StreamableLoading
{
    public static class WebRequestUtils
    {
        public static void SetCommonParameters(this UnityWebRequest unityWebRequest, in CommonLoadingArguments parameters)
        {
            unityWebRequest.timeout = parameters.Timeout;

            // Add more as needed
        }

        public static bool IsServerError(this UnityWebRequest request) =>
            request is { responseCode: >= 500 and < 600 };

        public static bool IsTimedOut(this UnityWebRequest request) =>
            request is { error: "Request timeout" };

        public static bool IsAborted(this UnityWebRequest request) =>
            request is { result: UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError, error: "Request aborted" or "User Aborted" };
    }
}
