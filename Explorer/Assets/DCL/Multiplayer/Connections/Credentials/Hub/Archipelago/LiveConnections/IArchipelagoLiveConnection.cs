using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Threading;

namespace DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.LiveConnections
{
    public interface IArchipelagoLiveConnection
    {
        bool Connected();

        UniTask ConnectAsync(string adapterUrl, CancellationToken token);

        UniTask DisconnectAsync(CancellationToken token);

        /// <param name="data">takes the ownership for the data</param>
        /// <param name="token">cancellation token</param>
        /// <returns>returns a memory chunk ang gives the ownership for it</returns>
        UniTask SendAsync(MemoryWrap data, CancellationToken token);

        UniTask<MemoryWrap> ReceiveAsync(CancellationToken token);
    }

    public static class ArchipelagoLiveConnectionExtensions
    {
        public static async UniTask SendAsync<T>(this IArchipelagoLiveConnection connection, T message, IMemoryPool memoryPool, CancellationToken token) where T: IMessage
        {
            using var memory = memoryPool.Memory(message);
            message.WriteTo(memory);
            await connection.SendAsync(memory, token);
        }

        /// <summary>
        /// Takes ownership for the data and returns the ownership for the result
        /// </summary>
        public static async UniTask<MemoryWrap> SendAndReceiveAsync(this IArchipelagoLiveConnection archipelagoLiveConnection, MemoryWrap data, CancellationToken token)
        {
            await archipelagoLiveConnection.SendAsync(data, token);
            return await archipelagoLiveConnection.ReceiveAsync(token);
        }

        public static async UniTask<MemoryWrap> SendAndReceiveAsync<T>(this IArchipelagoLiveConnection connection, T message, IMemoryPool memoryPool, CancellationToken token) where T: IMessage
        {
            using var memory = memoryPool.Memory(message);
            message.WriteTo(memory);
            var result = await connection.SendAndReceiveAsync(memory, token);
            return result;
        }

        public static async UniTask WaitDisconnect(this IArchipelagoLiveConnection connection, CancellationToken token)
        {
            while (token.IsCancellationRequested == false && connection.Connected())
                await UniTask.Yield();
        }
    }
}
