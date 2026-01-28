using Cysharp.Threading.Tasks;
using DCL.SocialService;
using Google.Protobuf;
using rpc_csharp;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Unity.PerformanceTesting;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    public class PerformanceTestSocialService : IRPCSocialServices
    {
        internal const string SEND_REQUEST_MARKER = "RPC.UnaryProcedure";

        internal readonly SampleGroup sendRequest = new (SEND_REQUEST_MARKER, SampleUnit.Microsecond);

        private readonly IRPCSocialServices core;

        private readonly ConcurrentDictionary<uint, long> requests = new ();

        public RpcClient? Client => core.Client;

        public bool WarmingUp { private get; set; }

        public PerformanceTestSocialService(IRPCSocialServices core)
        {
            this.core = core;
        }

        public void Dispose() =>
            core.Dispose();

        public RpcClientModule Module()
        {
            ListenToUnaryProcedures(core.Client);
            return core.Module();
        }

        public UniTask EnsureRpcConnectionAsync(int connectionRetries, CancellationToken ct) =>
            core.EnsureRpcConnectionAsync(connectionRetries, ct);

        private void ListenToUnaryProcedures(RpcClient? client)
        {
            if (client == null) return;

            client.dispatcher.RequestResponseStarted -= OnRequestResponseStarted;
            client.dispatcher.RequestResponseStarted += OnRequestResponseStarted;

            client.dispatcher.RequestResponseFinished -= OnRequestResponseFinished;
            client.dispatcher.RequestResponseFinished += OnRequestResponseFinished;
        }

        private void OnRequestResponseFinished(IMessage message, uint uid)
        {
            if (!requests.TryRemove(uid, out long startedAt))
                return;

            Measure.Custom(sendRequest, PerformanceTestWebRequestsAnalytics.ToMs(startedAt, Stopwatch.GetTimestamp()));
        }

        private void OnRequestResponseStarted(IRpcMessage obj, uint uid)
        {
            if (WarmingUp) return;

            requests[uid] = Stopwatch.GetTimestamp();
        }
    }
}
