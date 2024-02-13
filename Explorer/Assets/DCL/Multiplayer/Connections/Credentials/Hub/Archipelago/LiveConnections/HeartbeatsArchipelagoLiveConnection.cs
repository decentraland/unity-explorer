using Cysharp.Threading.Tasks;
using Decentraland.Kernel.Comms.V3;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.LiveConnections
{
    public class HeartbeatsArchipelagoLiveConnection : IArchipelagoLiveConnection //TODO
    {
        private readonly IArchipelagoLiveConnection origin;
        private readonly IMemoryPool memoryPool;
        private readonly TimeSpan interval;

        public HeartbeatsArchipelagoLiveConnection(IArchipelagoLiveConnection origin) : this(
            origin,
            new ArrayMemoryPool(),
            TimeSpan.FromSeconds(1)
        ) { }

        public HeartbeatsArchipelagoLiveConnection(IArchipelagoLiveConnection origin, IMemoryPool memoryPool, TimeSpan interval)
        {
            this.origin = origin;
            this.memoryPool = memoryPool;
            this.interval = interval;
        }

        public async UniTaskVoid LaunchHeartbeats(CancellationToken token)
        {
            var message = new Heartbeat();

            while (token.IsCancellationRequested == false)
            {
                await UniTask.Delay(interval, cancellationToken: token);

                //message.Position= new Position() //TODO position
                await origin.SendAndReceiveAsync(message, memoryPool, token);
            }
        }

        public bool Connected() =>
            origin.Connected();

        public UniTask ConnectAsync(string adapterUrl, CancellationToken token) =>
            origin.ConnectAsync(adapterUrl, token);

        public UniTask DisconnectAsync(CancellationToken token) =>
            origin.DisconnectAsync(token);

        public UniTask<MemoryWrap> ReceiveAsync(CancellationToken token) =>
            origin.ReceiveAsync(token);

        public UniTask SendAsync(MemoryWrap data, CancellationToken token) =>
            origin.SendAsync(data, token);
    }
}
