using CDPBridges;
using Collections.Pooled;
using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using DCL.WebRequests.ChromeDevtool;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Networking;
using UnityEngine.Pool;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Reserve for the future to deeply analyze ongoing web-requests
    /// </summary>
    public interface IWebRequestsAnalyticsContainer
    {
        IDictionary<Type, Func<IRequestMetric>> GetTrackedMetrics();

        IReadOnlyList<IRequestMetric>? GetMetric(Type requestType);

        internal void OnRequestStarted<T>(T request) where T: ITypedWebRequest;

        internal void OnRequestFinished<T>(T request) where T: ITypedWebRequest;

        public static readonly IWebRequestsAnalyticsContainer DEFAULT = new WebRequestsAnalyticsContainer();
    }

    public interface IMutableWebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        WebRequestsAnalyticsContainer AddTrackedMetric<T>() where T: class, IRequestMetric, new();
    }

    public static class WebRequestsAnalyticsExtensions
    {
        internal static async UniTask WithAnalyticsAsync<T>(this T request, IWebRequestsAnalyticsContainer analyticsContainer, UniTask innerTask) where T: ITypedWebRequest
        {
            try
            {
                analyticsContainer.OnRequestStarted(request);
                await innerTask;
            }
            finally
            {
                // Regardless of the exception at this moment request is not disposed of, and, thus, can be read from
                analyticsContainer.OnRequestFinished(request);
            }
        }

        internal static async UniTask WithChromeDevtools<TWebRequest, TWebRequestArgs>(this UniTask innerTask, RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, UnityWebRequest uwr, ChromeDevtoolProtocolClient chromeDevtoolProtocolClient) where TWebRequestArgs: struct where TWebRequest: struct, ITypedWebRequest
        {
            NotifyWebRequestScope? notifyScope = null;

            PooledObject<Dictionary<string, string>> pooledObject;

            if (chromeDevtoolProtocolClient.Status is BridgeStatus.HasListeners)
            {
                pooledObject = envelope.Headers(out Dictionary<string, string> headers);
                notifyScope = chromeDevtoolProtocolClient.NotifyWebRequestStart(uwr.url, uwr.method!, headers);
            }
            else

                // Don't waste memory and CPU to rent a dictionary if dev tools are disabled
                pooledObject = PoolExtensions.EmptyPooledObject<Dictionary<string, string>>();

            using PooledObject<Dictionary<string, string>> _ = pooledObject;

            try
            {
                await innerTask;

                if (notifyScope.HasValue)
                {
                    int statusCode = (int)uwr.responseCode;

                    // TODO avoid allocation?
                    Dictionary<string, string>? responseHeaders = uwr.GetResponseHeaders();

                    string mimeType = uwr.GetRequestHeader("Content-Type") ?? "application/octet-stream";
                    int encodedDataLength = (int)uwr.downloadedBytes;
                    notifyScope.Value.NotifyFinishAsync(statusCode, responseHeaders, mimeType, encodedDataLength).Forget();
                }
            }
            catch (OperationCanceledException)
            {
                notifyScope?.NotifyFailed("Cancelled", true);
                throw;
            }
            catch (UnityWebRequestException unityWebRequestException)
            {
                notifyScope?.NotifyFailed(unityWebRequestException.Error, false);
                throw;
            }
            catch (Exception e)
            {
                notifyScope?.NotifyFailed($"Engine exception: {e.Message}", false);
                throw;
            }
        }
    }
}
