using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Utility.Types;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    public interface IArchipelagoLiveConnection
    {
        bool IsConnected { get; }

        UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token);

        UniTask<Result> DisconnectAsync(CancellationToken token);

        /// <param name="data">takes the ownership for the data</param>
        /// <param name="token">cancellation token</param>
        /// <returns>returns a memory chunk ang gives the ownership for it</returns>
        UniTask<EnumResult<ResponseError>> SendAsync(MemoryWrap data, CancellationToken token);

        UniTask<EnumResult<MemoryWrap, ResponseError>> ReceiveAsync(CancellationToken token);

        enum ResponseError
        {
            MessageError,
            ConnectionClosed,
        }
    }

    public static class ArchipelagoLiveConnectionExtensions
    {
        public static LogArchipelagoLiveConnection WithLog(this IArchipelagoLiveConnection connection) =>
            new (connection);

        public static async UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendAsync<T>(this IArchipelagoLiveConnection connection, T message, IMemoryPool memoryPool, CancellationToken token) where T: IMessage
        {
            using MemoryWrap memory = memoryPool.Memory(message);
            message.WriteTo(memory);
            return await connection.SendAsync(memory, token);
        }

        /// <summary>
        ///     Takes ownership for the data and returns the ownership for the result
        /// </summary>
        public static async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> SendAndReceiveAsync(this IArchipelagoLiveConnection archipelagoLiveConnection, MemoryWrap data, CancellationToken token)
        {
            await archipelagoLiveConnection.SendAsync(data, token);
            return await archipelagoLiveConnection.ReceiveAsync(token);
        }

        public static async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> SendAndReceiveAsync<T>(this IArchipelagoLiveConnection connection, T message, IMemoryPool memoryPool, CancellationToken token) where T: IMessage
        {
            using MemoryWrap memory = memoryPool.Memory(message);
            message.WriteTo(memory);
            var result = await connection.SendAndReceiveAsync(memory, token);
            return result;
        }

        public static async UniTask WaitDisconnectAsync(this IArchipelagoLiveConnection connection, CancellationToken token)
        {
            while (token.IsCancellationRequested == false && connection.IsConnected)
                await UniTask.Yield();
        }
    }
}
