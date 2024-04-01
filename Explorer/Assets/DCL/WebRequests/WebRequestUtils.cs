using Cysharp.Threading.Tasks;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public static class WebRequestUtils
    {
        public static async UniTask<T> WithCustomExceptionAsync<T>(this UniTask<T> webRequestFlow, Func<UnityWebRequestException, Exception> newExceptionFactoryMethod) where T: ITypedWebRequest
        {
            try { return await webRequestFlow; }
            catch (UnityWebRequestException e) { throw newExceptionFactoryMethod(e); }
        }

        public static bool IsIrrecoverableError(this UnityWebRequestException exception, int attemptLeft) =>
            attemptLeft <= 0 || ((exception.IsAborted() || exception.IsServerError() || exception.IsMethodNotAllowed()) && !exception.IsUnableToCompleteSSLConnection());

        public static bool IsUnableToCompleteSSLConnection(this UnityWebRequestException exception)
        {
            // fixes frequent editor exception
#if UNITY_EDITOR
            return exception.Message.Contains("Unable to complete SSL connection");
#else
            return false;
#endif
        }

        public static bool IsMethodNotAllowed(this UnityWebRequestException exception) =>
            exception is { ResponseCode: 405 };

        public static bool IsServerError(this UnityWebRequestException exception) =>
            exception is { ResponseCode: >= 500 and < 600 };

        public static bool IsTimedOut(this UnityWebRequestException exception) =>
            exception is { Error: "Request timeout" };

        public static bool IsAborted(this UnityWebRequestException exception) =>
            exception is { Result: UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError, Error: "Request aborted" or "User Aborted" };
    }
}
