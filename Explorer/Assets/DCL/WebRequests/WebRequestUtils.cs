using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.WebRequests
{
    public static class WebRequestUtils
    {
        public const string CANNOT_CONNECT_ERROR = "Cannot connect to destination host";

        public const int BAD_REQUEST = 400;
        public const int FORBIDDEN_ACCESS = 403;
        public const int NOT_FOUND = 404;

        public static UniTask<TResult?> SuppressAnyExceptionWithFallback<TResult>(this UniTask<TResult?> coreOp,
            TResult fallbackValue, ReportData? reportData = null) =>
            coreOp.SuppressExceptionWithFallbackAsync(fallbackValue, SuppressExceptionWithFallback.Behaviour.SuppressAnyException, reportData);

        public static async UniTask<TResult?> SuppressExceptionWithFallbackAsync<TResult>(this UniTask<TResult?> coreOp,
            TResult fallbackValue,
            SuppressExceptionWithFallback.Behaviour behaviour = SuppressExceptionWithFallback.Behaviour.Default,
            ReportData? reportData = null)
        {
            try { return await coreOp; }
            catch (WebRequestException e)
            {
                ReportException(e);
                return fallbackValue;
            }
            catch (OperationCanceledException) when (EnumUtils.HasFlag(behaviour, SuppressExceptionWithFallback.Behaviour.SuppressCancellation)) { return fallbackValue; }
            catch (Exception e) when (EnumUtils.HasFlag(behaviour, SuppressExceptionWithFallback.Behaviour.SuppressAnyException))
            {
                ReportException(e);
                return fallbackValue;
            }

            void ReportException(Exception e)
            {
                if (reportData != null)
                    ReportHub.LogException(e, reportData.Value);
            }
        }

        public static SuppressExceptionWithFallback<TCoreOp, TWebRequest, TResult> SuppressExceptionsWithFallback<TCoreOp, TWebRequest, TResult>(this TCoreOp coreOp, TResult fallbackValue, SuppressExceptionWithFallback.Behaviour behaviour = SuppressExceptionWithFallback.Behaviour.Default, ReportData? reportContext = null) where TWebRequest: struct, ITypedWebRequest where TCoreOp: IWebRequestOp<TWebRequest, TResult> =>
            new (coreOp, fallbackValue, behaviour, reportContext);

        public static SuppressExceptionWithFallback<GetTextureWebRequest.CreateTextureOp, GetTextureWebRequest, IOwnedTexture2D> SuppressExceptionsWithFallback(
            this GetTextureWebRequest.CreateTextureOp coreOp,
            OwnedTexture2D fallbackValue,
            SuppressExceptionWithFallback.Behaviour behaviour = SuppressExceptionWithFallback.Behaviour.Default,
            ReportData? reportContext = null
        ) =>
            new (coreOp, fallbackValue, behaviour, reportContext);

        public static async UniTask<T> WithCustomExceptionAsync<T>(this UniTask<T> webRequestFlow, Func<WebRequestException, Exception> newExceptionFactoryMethod)
        {
            try { return await webRequestFlow; }
            catch (WebRequestException e) { throw newExceptionFactoryMethod(e); }
        }

        public static bool IsIrrecoverableError(this UnityWebRequestException exception, int attemptLeft) =>
            attemptLeft <= 0 || exception.ResponseCode is NOT_FOUND || ((exception.IsAborted() || exception.IsServerError()) && !exception.IsUnableToCompleteSSLConnection());

        public static bool IsUnableToCompleteSSLConnection(this UnityWebRequestException exception)
        {
            // fixes frequent editor exception
#if UNITY_EDITOR
            return exception.Message.Contains("Unable to complete SSL connection");
#else
            return false;
#endif
        }

        public static bool IsServerError(this WebRequestException exception) =>
            exception is { ResponseCode: >= 500 and < 600 };

        public static bool IsTimedOut(this UnityWebRequestException exception) =>
            exception is { Error: "Request timeout" };

        public static bool IsAborted(this UnityWebRequestException exception) =>
            exception is { Result: UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError, Error: "Request aborted" or "User Aborted" };

        public static string GetResponseContentType(this IWebRequest unityWebRequest) =>
            unityWebRequest.Response.GetHeader(WebRequestHeaders.CONTENT_TYPE_HEADER);

        public static string GetResponseContentEncoding(this IWebRequest unityWebRequest) =>
            unityWebRequest.Response.GetHeader("Content-Encoding");

        /// <summary>
        /// Does nothing with the web request
        /// </summary>
        public readonly struct NoOp<TWebRequest> : IWebRequestOp<TWebRequest, NoResult> where TWebRequest: struct, ITypedWebRequest
        {
            public UniTask<NoResult> ExecuteAsync(TWebRequest webRequest, CancellationToken ct) =>
                UniTask.FromResult(new NoResult());
        }

        public readonly struct NoResult { }
    }
}
