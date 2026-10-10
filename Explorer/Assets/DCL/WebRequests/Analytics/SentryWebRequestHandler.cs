using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics;
using Sentry;
using Sentry.Unity;
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Networking;
using UnityEngine.Pool;
using static DCL.PerformanceAndDiagnostics.SentryTransactionMapping<UnityEngine.Networking.UnityWebRequest>;

namespace DCL.WebRequests.Analytics
{
    public class SentryWebRequestHandler : IWebRequestAnalyticsHandler
    {
        private readonly SentryWebRequestSampler sampler;

        private readonly ProfilerMarker onRequestStarted;
        private readonly ProfilerMarker onRequestFinished;
        private readonly ProfilerMarker onProcessDataStarted;
        private readonly ProfilerMarker onProcessDataFinished;
        private readonly ProfilerMarker onException;

        public SentryWebRequestHandler(SentryWebRequestSampler sampler)
        {
            this.sampler = sampler;

            onRequestStarted = new ProfilerMarker($"{nameof(SentryWebRequestHandler)}.{nameof(OnRequestStarted)}");
            onRequestFinished = new ProfilerMarker($"{nameof(SentryWebRequestHandler)}.{nameof(OnRequestFinished)}");
            onProcessDataStarted = new ProfilerMarker($"{nameof(SentryWebRequestHandler)}.OnProcessDataStarted");
            onProcessDataFinished = new ProfilerMarker($"{nameof(SentryWebRequestHandler)}.{nameof(OnProcessDataFinished)}");
            onException = new ProfilerMarker($"{nameof(SentryWebRequestHandler)}.{nameof(OnException)}");
        }

        public void Update(float dt) { }

        public void OnBeforeBudgeting<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request) where T: struct, ITypedWebRequest where TWebRequestArgs: struct { }

        public void OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request, DateTime startedAt) where T: struct, ITypedWebRequest where TWebRequestArgs: struct
        {
            using ProfilerMarker.AutoScope __ = onRequestStarted.Auto();

            // Fast path to ignore files
            if (envelope.CommonArguments.URL.IsFile()) return;

            // Before the decision of sampling has been made, minimize allocations
            // unlike UWR.url, envelope.CommonArguments.URL is already allocated
            // Transaction context can't be reused as it contains several closed fields, so the allocation is inevitable
            var transactionContext = new TransactionContext(envelope.CommonArguments.URL, OpenTelemetrySemantics.OperationHttpClient, nameSource: TransactionNameSource.Url);

            // We will receive the name of the transaction from the sampler
            (PooledObject<Dictionary<string, object>> pooled, SentryWebRequestSampler.SamplingContext context) = sampler.PoolContext(out Dictionary<string, object> raw);
            using PooledObject<Dictionary<string, object>> _ = pooled;

            UnityWebRequest uwr = request.UnityWebRequest;

            ITransactionTracer? transaction = Instance.StartSentryTransaction(uwr, transactionContext, raw!);

            if (transaction is { IsSampled: true } && context.TransactionName is { } transactionName
                                                   && SentryWebRequestSampler.TryParseUrlParts(envelope.CommonArguments.URL, transactionName, request.UnityWebRequest.method, out SentryWebRequestSampler.OpenTelemetryUrlParts urlParts))
            {
                transaction.Name = urlParts.TransactionName;

                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpRequestMethod, request.UnityWebRequest.method);
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpScheme, urlParts.Scheme);
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpHost, urlParts.Host);
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpTarget, urlParts.Target);
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpUrl, urlParts.Url);

                // add headers to the web request to support distributed tracing

                SentryTraceHeader traceHeader = transaction.GetTraceHeader();
                uwr.SetRequestHeader("sentry-trace", traceHeader.ToString());

                BaggageHeader? baggageHeader = SentrySdk.GetBaggage();

                if (baggageHeader != null)
                    uwr.SetRequestHeader("baggage", baggageHeader.ToString());
            }
        }

        public void OnRequestFinished<T>(T request, TimeSpan duration) where T: ITypedWebRequest
        {
            using ProfilerMarker.AutoScope _ = onRequestFinished.Auto();

            UnityWebRequest uwr = request.UnityWebRequest;

            if (Instance.TryGet(uwr, out ITransactionTracer transaction))
            {
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpRequestContentLength, uwr.uploadedBytes);
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpResponseContentLength, uwr.downloadedBytes);
            }

            const string OP_NAME = "process_data";

            using ProfilerMarker.AutoScope __ = onProcessDataStarted.Auto();

            // Add a child span to instrument data processing
            string spanName = typeof(T).Name;
            Instance.StartSpan(request.UnityWebRequest, new SpanData { Depth = 1, SpanName = spanName, SpanOperation = OP_NAME });
        }

        /// <summary>
        ///     It will be called if the request has successfully finished along with all data processing
        /// </summary>
        public void OnProcessDataFinished<T>(T request) where T: ITypedWebRequest
        {
            using ProfilerMarker.AutoScope _ = onProcessDataFinished.Auto();

            Instance.EndCurrentSpan(request.UnityWebRequest);
            Instance.EndTransaction(request.UnityWebRequest);
        }

        public void OnException(UnityWebRequestException unityWebRequestException, TimeSpan duration)
        {
            using ProfilerMarker.AutoScope _ = onException.Auto();

            // The exception will be attached to the corresponding transaction automatically

            if (Instance.TryGet(unityWebRequestException.UnityWebRequest, out ITransactionTracer transaction))
            {
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpResponseStatusCode, unityWebRequestException.ResponseCode);
                Instance.EndTransactionWithError(unityWebRequestException.UnityWebRequest, nameof(UnityWebRequestException), FromHttpStatusCode(unityWebRequestException.ResponseCode));
            }
        }

        public void OnException<T>(T request, Exception exception, TimeSpan duration) where T: ITypedWebRequest
        {
            using ProfilerMarker.AutoScope _ = onException.Auto();

            // The exception will be attached to the corresponding transaction automatically
            Instance.EndTransactionWithError(request.UnityWebRequest, $"{exception.GetType().Name}", exception: exception);
        }

        public void OnException<T>(T request, UnityWebRequestException exception, TimeSpan duration) where T: ITypedWebRequest { }

        /// <summary>
        ///     Copied from the internal class SpanStatusConverter
        /// </summary>
        private static SpanStatus FromHttpStatusCode(long code) =>
            code switch
            {
                < 400 => SpanStatus.Ok,

                400 => SpanStatus.FailedPrecondition,
                401 => SpanStatus.Unauthenticated,
                403 => SpanStatus.PermissionDenied,
                404 => SpanStatus.NotFound,
                409 => SpanStatus.AlreadyExists,
                429 => SpanStatus.ResourceExhausted,
                499 => SpanStatus.Cancelled,
                < 500 => SpanStatus.FailedPrecondition,

                500 => SpanStatus.InternalError,
                501 => SpanStatus.Unimplemented,
                503 => SpanStatus.Unavailable,
                504 => SpanStatus.DeadlineExceeded,
                < 600 => SpanStatus.InternalError,

                _ => SpanStatus.UnknownError,
            };
    }
}
