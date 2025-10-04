using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;

namespace DCL.SocialService
{
    /// <summary>
    ///     The base class for all RPC Social Services that communicate through <see cref="IRPCSocialServices" />
    /// </summary>
    public abstract class RPCSocialServiceBase : IDisposable
    {
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

        protected RPCSocialServiceBase(IRPCSocialServices rpcSocialServices, string reportCategory)
        {
            socialServiceRPC = rpcSocialServices;
            this.reportCategory = reportCategory;
        }

        protected async UniTask KeepServerStreamOpenAsync(Func<UniTask> openStreamFunc, CancellationToken ct)
        {
            ct = CancellationTokenSource.CreateLinkedTokenSource(ct, lifeTimeCts.Token).Token;

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
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, new ReportData(reportCategory, new ReportDebounce(serverStreamReportsDebouncer))); }
            }
        }

        public virtual void Dispose()
        {
            lifeTimeCts.Cancel();
        }
    }
}
