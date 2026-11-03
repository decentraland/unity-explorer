// TRUST_WEBGL_SYSTEM_TASKS_SAFETY_FLAG: desktop-only, System.Net.Sockets has no WebGL implementation
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Utility.Networking
{
    /// <summary>
    ///     Internet connectivity confirmed by a TCP connect to a highly-available host, because no
    ///     OS-level signal works on desktop: Unity's <c>Application.internetReachability</c> always reports LAN there and the
    ///     Mono BCL stubs <c>NetworkInterface.GetIsNetworkAvailable</c> to <c>true</c>.
    /// </summary>
    /// <remarks>Thread-safe. Must not log through <c>ReportHub</c>, since it runs underneath the report pipeline.</remarks>
    public class InternetConnectivityMonitor : IDisposable
    {
        public const int HTTPS_PORT = 443;

        private static readonly TimeSpan DEFAULT_PROBE_TIMEOUT = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DEFAULT_RESULT_TTL = TimeSpan.FromSeconds(10);
        private static readonly Task<bool> ONLINE = Task.FromResult(true);
        private static readonly Task<bool> OFFLINE = Task.FromResult(false);

        private readonly string probeHost;
        private readonly int probePort;
        private readonly TimeSpan probeTimeout;
        private readonly TimeSpan resultTtl;
        private readonly object sync = new ();
        private readonly CancellationTokenSource lifetimeCts = new ();

        private Task<bool>? probeInFlight;
        private DateTime lastProbeUtc = DateTime.MinValue;
        private volatile bool isOnline = true;
        private bool disposed;

        /// <summary>The last confirmed state, online until proven otherwise.</summary>
        public bool IsOnline => isOnline;

        public InternetConnectivityMonitor(string probeHost, int probePort = HTTPS_PORT, TimeSpan? probeTimeout = null, TimeSpan? resultTtl = null)
        {
            this.probeHost = probeHost;
            this.probePort = probePort;
            this.probeTimeout = probeTimeout ?? DEFAULT_PROBE_TIMEOUT;
            this.resultTtl = resultTtl ?? DEFAULT_RESULT_TTL;
        }

        public void Dispose()
        {
            lock (sync) { disposed = true; }

            lifetimeCts.SafeCancelAndDispose();
        }

        /// <summary>The probe in flight, shared by concurrent callers, or the cached state while it is younger than the TTL.</summary>
        public Task<bool> RefreshIfStaleAsync()
        {
            lock (sync)
            {
                if (probeInFlight != null)
                    return probeInFlight;

                if (disposed || DateTime.UtcNow - lastProbeUtc < resultTtl)
                    return isOnline ? ONLINE : OFFLINE;

                // Task.Run never returns a completed task, so this assignment lands before RunProbeAsync clears it
                probeInFlight = RunProbeAsync();
                return probeInFlight;
            }
        }

        private async Task<bool> RunProbeAsync()
        {
            bool? reachable = await Task.Run(TryConnectAsync).ConfigureAwait(false);

            lock (sync)
            {
                if (reachable.HasValue)
                    isOnline = reachable.Value;

                lastProbeUtc = DateTime.UtcNow;
                probeInFlight = null;
            }

            return isOnline;
        }

        // Null when disposed mid-probe: nothing was learnt
        private async Task<bool?> TryConnectAsync()
        {
            using var client = new TcpClient();

            try
            {
                Task connect = client.ConnectAsync(probeHost, probePort);
                Task first = await Task.WhenAny(connect, Task.Delay(probeTimeout, lifetimeCts.Token)).ConfigureAwait(false);

                if (first != connect)
                {
                    // Disposing the client faults the abandoned connect, which must still be observed
                    _ = connect.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                    return lifetimeCts.IsCancellationRequested ? null : false;
                }

                await connect.ConfigureAwait(false);
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
