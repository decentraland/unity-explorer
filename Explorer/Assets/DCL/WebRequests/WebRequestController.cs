using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    public class WebRequestController : IWebRequestController
    {
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IRequestHub requestHub;
        private readonly string csvPath;
        private static readonly object csvLock = new object();

        public WebRequestController(
            IWebRequestsAnalyticsContainer analyticsContainer,
            IWeb3IdentityCache web3IdentityCache,
            IRequestHub requestHub)
        {
            this.analyticsContainer = analyticsContainer;
            this.web3IdentityCache = web3IdentityCache;
            this.requestHub = requestHub;

            // Set up CSV file path in persistent data path
            csvPath = Path.Combine(Application.persistentDataPath, "request_metrics.csv");

            // Create CSV header if file doesn't exist
            if (!File.Exists(csvPath))
            {
                lock (csvLock)
                {
                    if (!File.Exists(csvPath))
                    {
                        File.WriteAllText(csvPath,
                            "Timestamp,RequestType,URL,RequestTime,OperationTime,TotalTime,Status,AttemptNumber,ErrorMessage\n");
                    }
                }
            }
        }

        private void LogMetricsToCSV(
            string requestType,
            string url,
            long requestTime,
            long operationTime,
            string status,
            int attemptNumber,
            string errorMessage = "")
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var totalTime = requestTime + operationTime;

            // Sanitize values for CSV
            url = url.Replace(",", "%2C");
            errorMessage = errorMessage.Replace(",", ";").Replace("\n", " ").Replace("\r", "");

            var logLine = $"{timestamp},{requestType},{url},{requestTime},{operationTime},{totalTime},{status},{attemptNumber},{errorMessage}\n";

            lock (csvLock)
            {
                try
                {
                    File.AppendAllText(csvPath, logLine, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    ReportHub.LogError(
                        null,
                        $"Failed to write to metrics CSV: {ex.Message}"
                    );
                }
            }
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(
            RequestEnvelope<TWebRequest, TWebRequestArgs> envelope,
            TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            await using ExecuteOnMainThreadScope scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();

            int attemptsLeft = envelope.CommonArguments.TotalAttempts();
            int currentAttempt = 1;
            var stopwatch = new Stopwatch();

            // ensure disposal of headersInfo
            using RequestEnvelope<TWebRequest, TWebRequestArgs> _ = envelope;

            while (attemptsLeft > 0)
            {
                TWebRequest request = envelope.InitializedWebRequest(web3IdentityCache);

                // No matter what we must release UnityWebRequest, otherwise it crashes in the destructor
                using UnityWebRequest wr = request.UnityWebRequest;

                try
                {
                    stopwatch.Restart();
                    await request.WithAnalyticsAsync(analyticsContainer, request.SendRequest(envelope.Ct));
                    var requestTime = stopwatch.ElapsedMilliseconds;

                    ReportHub.Log(
                        envelope.ReportData,
                        $"Request timing for {typeof(TWebRequest).Name} to {envelope.CommonArguments.URL}: {requestTime}ms"
                    );

                    stopwatch.Restart();
                    var result = await op.ExecuteAsync(request, envelope.Ct);
                    var operationTime = stopwatch.ElapsedMilliseconds;

                    ReportHub.Log(
                        envelope.ReportData,
                        $"Operation timing for {typeof(TWebRequest).Name}: {operationTime}ms"
                    );

                    // Log successful request to CSV
                    LogMetricsToCSV(
                        typeof(TWebRequest).Name,
                        envelope.CommonArguments.URL,
                        requestTime,
                        operationTime,
                        "Success",
                        currentAttempt
                    );

                    return result;
                }
                catch (UnityWebRequestException exception)
                {
                    var failureTime = stopwatch.ElapsedMilliseconds;

                    if (envelope.ShouldIgnoreResponseError(exception.UnityWebRequest!))
                    {
                        LogMetricsToCSV(
                            typeof(TWebRequest).Name,
                            envelope.CommonArguments.URL,
                            failureTime,
                            0,
                            "Ignored",
                            currentAttempt,
                            "Response error ignored"
                        );
                        return default(TResult);
                    }

                    attemptsLeft--;
                    currentAttempt++;

                    if (!envelope.SuppressErrors)
                    {
                        ReportHub.LogError(
                            envelope.ReportData,
                            $"Exception occurred on loading {typeof(TWebRequest).Name} from {envelope.CommonArguments.URL} with {envelope}\n" +
                            $"Request failed after {failureTime}ms\n" +
                            $"Attempts Left: {attemptsLeft}"
                        );
                    }

                    // Log failed request to CSV
                    LogMetricsToCSV(
                        typeof(TWebRequest).Name,
                        envelope.CommonArguments.URL,
                        failureTime,
                        0,
                        "Failed",
                        currentAttempt - 1,
                        exception.Message
                    );

                    if (exception.Message.Contains(WebRequestUtils.CANNOT_CONNECT_ERROR))
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                    }

                    if (envelope.CommonArguments.AttemptsDelayInMilliseconds() > 0)
                        await UniTask.Delay(TimeSpan.FromMilliseconds(envelope.CommonArguments.AttemptsDelayInMilliseconds()));

                    if (exception.IsIrrecoverableError(attemptsLeft))
                        throw;
                }
            }

            throw new Exception($"{nameof(WebRequestController)}: Unexpected code path!");
        }

        IRequestHub IWebRequestController.requestHub => requestHub;
    }
}
