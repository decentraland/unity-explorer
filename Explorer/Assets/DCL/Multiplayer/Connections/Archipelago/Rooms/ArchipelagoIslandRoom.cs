using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Rooms;
using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;
using Utility.Types;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class ArchipelagoIslandRoom : IArchipelagoIslandRoom
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IArchipelagoSignFlow signFlow;
        private readonly ICharacterObject characterObject;
        private readonly IConnectiveRoom connectiveRoom;
        private readonly ICurrentAdapterAddress currentAdapterAddress;

        private ConnectToRoomAsyncDelegate? connectToRoomAsyncDelegate;

        public ArchipelagoIslandRoom(ICharacterObject characterObject, IWeb3IdentityCache web3IdentityCache, IMultiPool multiPool, ICurrentAdapterAddress currentAdapterAddress) : this(
            web3IdentityCache,
            new LiveConnectionArchipelagoSignFlow(
                new WebSocketArchipelagoLiveConnection(
                    () => new ClientWebSocket(),
                    new ArrayMemoryPool(ArrayPool<byte>.Create())
                ).WithLog(),
                new ArrayMemoryPool(ArrayPool<byte>.Create()),
                multiPool
            ).WithLog(),
            characterObject,
            currentAdapterAddress
        ) { }

        public ArchipelagoIslandRoom(
            IWeb3IdentityCache web3IdentityCache,
            IArchipelagoSignFlow signFlow,
            ICharacterObject characterObject,
            ICurrentAdapterAddress currentAdapterAddress
        )
        {
            this.web3IdentityCache = web3IdentityCache;
            this.signFlow = signFlow;
            this.characterObject = characterObject;
            this.currentAdapterAddress = currentAdapterAddress;

            connectiveRoom = new RenewableConnectiveRoom(
                () => new ConnectiveRoom(
                    PrewarmAsync,
                    SendHeartbeatAsync,
                    nameof(ArchipelagoIslandRoom)
                )
            );
        }

        public UniTask<bool> StartAsync() =>
            connectiveRoom.StartAsync();

        public UniTask StopAsync() =>
            UniTask.WhenAll(

                //signFlow.DisconnectAsync(CancellationToken.None),
                connectiveRoom.StopAsync()
            );

        public IConnectiveRoom.State CurrentState() =>
            connectiveRoom.CurrentState();

        public IRoom Room() =>
            connectiveRoom.Room();

        private async UniTask PrewarmAsync(CancellationToken token)
        {
            const int ATTEMPTS = 3;
            Result result = Result.ErrorResult("Cannot connect to Archipelago");

            for (var i = 0; i < ATTEMPTS; i++)
            {
                result = await ConnectToArchipelagoAsync(token);

                if (result.Success)
                    break;
            }

            if (result.Success == false)
                throw new InvalidOperationException(result.ErrorMessage!);

            signFlow.StartListeningForConnectionStringAsync(newString => OnNewConnectionString(newString, token), token);
        }

        private async UniTask SendHeartbeatAsync(ConnectToRoomAsyncDelegate connectDelegate, DisconnectCurrentRoomAsyncDelegate disconnectCurrentRoomAsyncDelegate, CancellationToken token)
        {
            connectToRoomAsyncDelegate = connectDelegate;
            await UniTask.SwitchToMainThread(token);
            Vector3 position = characterObject.Position;
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();

            var result = await signFlow.SendHeartbeatAsync(position, token);

            if (result.Success == false)
                ReportHub.LogWarning(ReportCategory.ARCHIPELAGO_REQUEST, $"Cannot send heartbeat, connection is closed: {result.ErrorMessage}");
        }

        private void OnNewConnectionString(string connectionString, CancellationToken token)
        {
            if (CurrentState() is IConnectiveRoom.State.Stopped) throw new InvalidOperationException("Room is not running");
            connectToRoomAsyncDelegate.EnsureNotNull("Connection delegate is not passed yet");
            connectToRoomAsyncDelegate!(connectionString, token).Forget();
        }

        private async UniTask<Result> ConnectToArchipelagoAsync(CancellationToken token)
        {
            string adapterUrl = currentAdapterAddress.AdapterUrl();
            Result<string> welcomePeerId = await WelcomePeerIdAsync(adapterUrl, token);

            if (welcomePeerId.Success == false)
            {
                var message = $"Cannot connect to archipelago, WelcomePeerId is invalid: {welcomePeerId.ErrorMessage}";
                ReportHub.LogError(ReportCategory.ARCHIPELAGO_REQUEST, message);
                return Result.ErrorResult(message);
            }

            return Result.SuccessResult();
        }

        private async UniTask<Result<string>> WelcomePeerIdAsync(string adapterUrl, CancellationToken token)
        {
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();
            IWeb3Identity identity = web3IdentityCache.EnsuredIdentity();
            await signFlow.EnsureConnectedAsync(adapterUrl, token);
            string ethereumAddress = identity.Address;
            var messageForSignResult = await signFlow.MessageForSignAsync(ethereumAddress, token);

            if (messageForSignResult.Success == false ||
                !HandshakePayloadIsValid(messageForSignResult.Value))
                return Result<string>.ErrorResult($"Cannot obtain a message to sign a welcome peer: {messageForSignResult.ErrorMessage}");

            string signedMessage = identity.Sign(messageForSignResult.Value).ToJson();
            ReportHub.Log(ReportCategory.ARCHIPELAGO_REQUEST, $"Signed message: {signedMessage}");
            return await signFlow.WelcomePeerIdAsync(signedMessage, token);
        }

        private bool HandshakePayloadIsValid(string payload)
        {
            if (!payload.StartsWith("dcl-"))
                return false;

            ReadOnlySpan<char> span = payload.AsSpan(4);
            return span.IndexOf(':') == -1;
        }
    }
}
