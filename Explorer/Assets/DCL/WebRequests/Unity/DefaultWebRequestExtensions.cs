using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using static DCL.WebRequests.WebRequestUtils;

namespace DCL.WebRequests
{
    public static class DefaultWebRequestExtensions
    {
        public static bool IsIrrecoverableError(this UnityWebRequestException exception, IWebRequest adapter, int attemptLeft) =>
            attemptLeft <= 0 || exception.ResponseCode is NOT_FOUND || ((adapter.IsAborted || adapter.IsServerError()) && !exception.IsUnableToCompleteSSLConnection());

        public static bool IsUnableToCompleteSSLConnection(this UnityWebRequestException exception)
        {
            // fixes frequent editor exception
#if UNITY_EDITOR
            return exception.Message.Contains("Unable to complete SSL connection");
#else
            return false;
#endif
        }
    }
}
