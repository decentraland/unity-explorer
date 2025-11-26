using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Sentry;
using System;
using System.Net.WebSockets;
using System.Threading;

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
                    await openStreamFunc().AttachExternalCancellation(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (WebSocketException e)
                {
                    retryAttempt++;

                    SentrySdk.AddBreadcrumb($"WebSocketException reason was WebSocketErrorCode: {e.WebSocketErrorCode.ToString()} "
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
                catch (Exception e)
                {
                    ReportHub.LogException(e, new ReportData(reportCategory, new ReportDebounce(serverStreamReportsDebouncer)));

                    try { await WaitNextRetryAsync(retryAttempt, ct); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private async UniTask WaitNextRetryAsync(int retryAttempt, CancellationToken ct)
        {
            // Calculate exponential backoff delay
            int delaySeconds = BASE_RETRY_DELAY_SECONDS * (int)Math.Pow(2, retryAttempt - 1);
            ReportHub.Log(reportCategory, $"Retrying connection in {delaySeconds} seconds...");

            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);
        }
    }
}
