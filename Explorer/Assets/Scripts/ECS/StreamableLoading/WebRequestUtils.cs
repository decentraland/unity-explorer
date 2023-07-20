using Cysharp.Threading.Tasks;
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

        public static bool IsServerError(this UnityWebRequestException exception) =>
            exception is { ResponseCode: >= 500 and < 600 };

        public static bool IsTimedOut(this UnityWebRequestException exception) =>
            exception is { Error: "Request timeout" };

        public static bool IsAborted(this UnityWebRequestException exception) =>
            exception is { Result: UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError, Error: "Request aborted" or "User Aborted" };
    }
}
