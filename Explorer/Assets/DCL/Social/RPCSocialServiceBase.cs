using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Sentry;
using System;
using System.Threading;
using Utility.Networking;

namespace DCL.SocialService
{
    /// <summary>
    ///     The base class for all RPC Social Services that communicate through <see cref="IRPCSocialServices" />
    /// </summary>
    public abstract class RPCSocialServiceBase : IDisposable
    {
        /// <summary>
        ///     Maximum number of retry attempts for server stream connection
        /// </summary>
        private const int MAX_RETRY_ATTEMPTS = 5;

        /// <summary>
        ///     Base delay in seconds between retry attempts (will be exponentially increased)
        /// </summary>
        private const int BASE_RETRY_DELAY_SECONDS = 2;

        private const int MAX_RETRY_DELAY_SECONDS = 60;

        // A stream open shorter than this was likely an instant server-side rejection (e.g. duplicate)
        private const int STABLE_STREAM_SECONDS = 30;

        public class ServerStreamReportsDebouncer : FrameDebouncer
        {
            public ServerStreamReportsDebouncer() : base(1)
            {
                // Tasks can be distributed across 2 frames so the threshold distance is 1 frame
            }

            public override ReportHandler AppliedTo => ReportHandler.Sentry;
        }

        protected readonly ServerStreamReportsDebouncer serverStreamReportsDebouncer = new ();

        protected readonly CancellationTokenSource lifeTimeCts = new ();

        protected readonly IRPCSocialServices socialServiceRPC;
        protected readonly string reportCategory;
        private readonly int maxRetries;

        protected RPCSocialServiceBase(IRPCSocialServices rpcSocialServices, string reportCategory,
            int maxRetries = MAX_RETRY_ATTEMPTS)
        {
            socialServiceRPC = rpcSocialServices;
            this.reportCategory = reportCategory;
            this.maxRetries = maxRetries;
        }

        public virtual void Dispose()
        {
            lifeTimeCts.Cancel();
        }

        protected async UniTask KeepServerStreamOpenAsync(Func<UniTask> openStreamFunc, CancellationToken ct)
        {
            ct = CancellationTokenSource.CreateLinkedTokenSource(ct, lifeTimeCts.Token).Token;

            var retryAttempt = 0;

            // We try to keep the stream open until cancellation is requested
            // If for any reason the rpc connection has a problem, we need to wait until it is restored, so we re-open the stream
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // It's an endless [background] loop
                    await socialServiceRPC.EnsureRpcConnectionAsync(int.MaxValue, ct);

                    DateTime streamOpenedAt = DateTime.UtcNow;
                    await openStreamFunc().AttachExternalCancellation(ct);

                    // A stream that completed instantly was likely rejected as a duplicate — back off.
                    // If it stayed open long enough to be genuine, reset the backoff counter.
                    if (DateTime.UtcNow - streamOpenedAt >= TimeSpan.FromSeconds(STABLE_STREAM_SECONDS))
                        retryAttempt = 0;

                    retryAttempt++;
                    await WaitNextRetryAsync(retryAttempt, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (WebSocketException e)
                {
                    retryAttempt++;

                    Sentry.Unity.SentrySdk.AddBreadcrumb($"WebSocketException reason was WebSocketErrorCode: {e.WebSocketErrorCode.ToString()} "
                                                         + $"ErrorCode: {e.ErrorCode.ToString()}", reportCategory, level: BreadcrumbLevel.Info);

                    var webSocketErrorCode = (WebSocketError)e.ErrorCode;

                    if (webSocketErrorCode is WebSocketError.Faulted
                        or WebSocketError.ConnectionClosedPrematurely
                        or WebSocketError.NativeError
                        or WebSocketError.HeaderError)
                        ReportHub.LogWarning(new ReportData(reportCategory, new ReportDebounce(serverStreamReportsDebouncer)),
                            $"WebSocketException {webSocketErrorCode} occurred while trying to keep rpc connection open, retrying..");
                    else
                        ReportHub.LogException(e, new ReportData(reportCategory, new ReportDebounce(serverStreamReportsDebouncer)));

                    try { await WaitNextRetryAsync(retryAttempt, ct); }
                    catch (OperationCanceledException) { break; }
                }
                catch (InvalidOperationException e) when (IsExpectedDisconnectException(e))
                {
                    // Expected during sign-out or transport close, exit gracefully
                    break;
                }
                catch (Exception e)
                {
                    retryAttempt++;

                    ReportHub.LogException(e, new ReportData(reportCategory, new ReportDebounce(serverStreamReportsDebouncer)));

                    try { await WaitNextRetryAsync(retryAttempt, ct); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private async UniTask WaitNextRetryAsync(int retryAttempt, CancellationToken ct)
        {
            // Exponential backoff, capped so it cannot grow without bound (computed in double to
            // avoid int overflow once retryAttempt gets large).
            int delaySeconds = (int)Math.Min(BASE_RETRY_DELAY_SECONDS * Math.Pow(2, retryAttempt - 1), MAX_RETRY_DELAY_SECONDS);
            ReportHub.Log(reportCategory, $"Retrying connection in {delaySeconds} seconds...");

            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);
        }

        /// <summary>
        ///     Checks if the exception is expected during sign-out or transport disconnection.
        ///     These exceptions should not be logged as errors.
        /// </summary>
        private static bool IsExpectedDisconnectException(InvalidOperationException e) =>
            e.Message.Contains("Transport", StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("Identity is not found", StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains("RPC", StringComparison.OrdinalIgnoreCase);
    }
}
