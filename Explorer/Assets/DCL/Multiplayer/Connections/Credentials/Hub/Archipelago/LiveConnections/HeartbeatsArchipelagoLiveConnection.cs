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
                await origin.Send(message, memoryPool, token);
            }
        }

        public bool Connected() =>
            origin.Connected();

        public UniTask Connect(string adapterUrl, CancellationToken token) =>
            origin.Connect(adapterUrl, token);

        public UniTask Disconnect(CancellationToken token) =>
            origin.Disconnect(token);

        public UniTask<MemoryWrap> Send(MemoryWrap data, CancellationToken token) =>
            origin.Send(data, token);
    }
}
