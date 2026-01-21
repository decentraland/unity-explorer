using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics;
using Sentry;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine.Pool;
using static DCL.PerformanceAndDiagnostics.SentryTransactionMapping<UnityEngine.Networking.UnityWebRequest>;

namespace DCL.WebRequests.Analytics
{
    public class SentryWebRequestHandler
    {
        private readonly SentryWebRequestSampler sampler;

        public SentryWebRequestHandler(SentryWebRequestSampler sampler)
        {
            this.sampler = sampler;
        }

        internal void OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request) where T: struct, ITypedWebRequest where TWebRequestArgs: struct
        {
            // Before the decision of sampling has been made, minimize allocations
            // unlike UWR.url, envelope.CommonArguments.URL is already allocated
            var transactionContext = new TransactionContext(envelope.CommonArguments.URL, OpenTelemetrySemantics.OperationHttpClient);

            // We will receive the name of the transaction from the sampler
            (PooledObject<Dictionary<string, object>> pooled, SentryWebRequestSampler.SamplingContext context) = sampler.PoolContext(out Dictionary<string, object> raw);
            using PooledObject<Dictionary<string, object>> _ = pooled;

            UnityWebRequest uwr = request.UnityWebRequest;

            ITransactionTracer? transaction = Instance.StartSentryTransaction(uwr, transactionContext, raw!);

            if (transaction is { IsSampled: true } && context.UrlParts is { } urlParts)
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

        internal void OnRequestFinished<T>(T request) where T: ITypedWebRequest
        {
            UnityWebRequest uwr = request.UnityWebRequest;

            if (Instance.TryGet(uwr, out ITransactionTracer transaction))
            {
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpRequestContentLength, uwr.uploadedBytes);
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpResponseContentLength, uwr.downloadedBytes);
            }
        }

        internal void OnProcessDataStarted<T>(T request) where T: ITypedWebRequest
        {
            const string OP_NAME = "process_data";

            // Add a child span to instrument data processing
            string spanName = typeof(T).Name;
            Instance.StartSpan(request.UnityWebRequest, new SpanData { Depth = 1, SpanName = spanName, SpanOperation = OP_NAME });
        }

        /// <summary>
        ///     It will be called if the request has successfully finished along with all data processing
        /// </summary>
        internal void OnProcessDataFinished<T>(T request) where T: ITypedWebRequest
        {
            Instance.EndCurrentSpan(request.UnityWebRequest);
        }

        internal void OnException(UnityWebRequestException unityWebRequestException)
        {
            // The exception will be attached to the corresponding transaction automatically

            if (Instance.TryGet(unityWebRequestException.UnityWebRequest, out ITransactionTracer transaction))
            {
                transaction.SetExtra(OpenTelemetrySemantics.AttributeHttpResponseStatusCode, unityWebRequestException.ResponseCode);
                Instance.EndTransactionWithError(unityWebRequestException.UnityWebRequest, nameof(UnityWebRequestException), FromHttpStatusCode(unityWebRequestException.ResponseCode));
            }
        }

        internal void OnException<T>(T request, Exception exception) where T: ITypedWebRequest
        {
            // The exception will be attached to the corresponding transaction automatically
            Instance.EndTransactionWithError(request.UnityWebRequest, $"{exception.GetType().Name}", exception: exception);
        }

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
