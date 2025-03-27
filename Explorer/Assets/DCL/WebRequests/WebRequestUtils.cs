using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Collections.Generic;
using System.Threading;
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
        public const int INTERNAL_SERVER_ERROR = 500;

        public static readonly ISet<int> IGNORE_NOT_FOUND = new HashSet<int> { NOT_FOUND };

        public static UniTask<TResult?> SuppressAnyExceptionWithFallback<TResult>(this UniTask<TResult?> coreOp,
            TResult fallbackValue, ReportData? reportData = null) =>
            coreOp.SuppressExceptionWithFallbackAsync(fallbackValue, SuppressExceptionWithFallback.Behaviour.SuppressAnyException, reportData);

        /// <summary>
        ///     If the exception is thrown reports it and converts it to a fallback value
        /// </summary>
        /// <param name="coreOp">The underlying async process including a web request execution</param>
        /// <param name="fallbackValue">Fallback Value</param>
        /// <param name="behaviour">Customization of the exceptions processing</param>
        /// <param name="reportData">
        ///     The exception will be logged only if Report Data is specified, otherwise it will be fully suppressed.
        ///     Note that <see cref="IWebRequestController" /> itself logs the exceptions
        /// </param>
        /// <param name="ignoreTheseErrorCodesOnly">
        ///     If Specified the exception will be suppressed only if the response code is in the set
        /// </param>
        /// <typeparam name="TResult">Returns the result of the fallback value</typeparam>
        /// <returns></returns>
        public static async UniTask<TResult?> SuppressExceptionWithFallbackAsync<TResult>(this UniTask<TResult?> coreOp,
            TResult fallbackValue,
            SuppressExceptionWithFallback.Behaviour behaviour = SuppressExceptionWithFallback.Behaviour.Default,
            ReportData? reportData = null,
            ISet<int>? ignoreTheseErrorCodesOnly = null)
        {
            try { return await coreOp; }
            catch (WebRequestException e)
            {
                // If ignore codes is specified but the request has finished with another code, re-throw the exception
                if (ignoreTheseErrorCodesOnly != null && !ignoreTheseErrorCodesOnly.Contains(e.ResponseCode))
                    throw;

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

        public static async UniTask<T> WithCustomExceptionAsync<T>(this UniTask<T> webRequestFlow, Func<WebRequestException, Exception> newExceptionFactoryMethod)
        {
            try { return await webRequestFlow; }
            catch (WebRequestException e) { throw newExceptionFactoryMethod(e); }
        }

        public static bool IsIrrecoverableError(this WebRequestException exception, int attemptLeft) =>
            attemptLeft <= 0 || exception.ResponseCode is NOT_FOUND || ((exception.IsAborted || exception.IsServerError()) && !exception.IsUnableToCompleteSSLConnection());

        public static bool IsUnableToCompleteSSLConnection(this WebRequestException exception)
        {
            // fixes frequent editor exception
#if UNITY_EDITOR
            return exception.Message.Contains("Unable to complete SSL connection");
#else
            return false;
#endif
        }

        public static bool IsServerError(this IWebRequest webRequest) =>
            webRequest.Response is { StatusCode: >= 500 and < 600 };

        public static string GetResponseContentType(this IWebRequest unityWebRequest) =>
            unityWebRequest.Response.GetHeader(WebRequestHeaders.CONTENT_TYPE_HEADER);

        public static string GetResponseContentEncoding(this IWebRequest unityWebRequest) =>
            unityWebRequest.Response.GetHeader("Content-Encoding");

        /// <summary>
        ///     Does nothing with the web request
        /// </summary>
        public readonly struct NoOp<TWebRequest> : IWebRequestOp<TWebRequest, NoResult> where TWebRequest: struct, ITypedWebRequest
        {
            public UniTask<NoResult> ExecuteAsync(TWebRequest webRequest, CancellationToken ct) =>
                UniTask.FromResult(new NoResult());
        }

        public readonly struct NoResult { }
    }
}
