using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Typing;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Rooms;
using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class ArchipelagoIslandRoom : IArchipelagoIslandRoom
    {
        private readonly IAdapterAddresses adapterAddresses;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IArchipelagoSignFlow signFlow;
        private readonly ICharacterObject characterObject;
        private readonly IConnectiveRoom connectiveRoom;
        private readonly string aboutUrl;

        private ConnectToRoomAsyncDelegate? connectToRoomAsyncDelegate;

        public ArchipelagoIslandRoom(ICharacterObject characterObject, IWebRequestController webRequestController, IWeb3IdentityCache web3IdentityCache, IMultiPool multiPool) : this(
            new RefinedAdapterAddresses(
                new WebRequestsAdapterAddresses(webRequestController)
            ),
            web3IdentityCache,
            new LiveConnectionArchipelagoSignFlow(
                new WebSocketArchipelagoLiveConnection(
                    new ClientWebSocket(),
                    new ArrayMemoryPool(ArrayPool<byte>.Shared!)
                ).WithLog(),
                new ArrayMemoryPool(ArrayPool<byte>.Shared!),
                multiPool
            ).WithLog(),
            characterObject,
            "https://realm-provider.decentraland.zone/main/about"
        ) { }

        public ArchipelagoIslandRoom(
            IAdapterAddresses adapterAddresses,
            IWeb3IdentityCache web3IdentityCache,
            IArchipelagoSignFlow signFlow,
            ICharacterObject characterObject,
            string aboutUrl
        )
        {
            this.adapterAddresses = adapterAddresses;
            this.web3IdentityCache = web3IdentityCache;
            this.signFlow = signFlow;
            this.characterObject = characterObject;
            this.aboutUrl = aboutUrl;

            connectiveRoom = new ConnectiveRoom(
                PrewarmAsync,
                SendHeartbeatAsync
            );
        }

        public void Start() =>
            connectiveRoom.Start();

        public void Stop() =>
            connectiveRoom.Stop();

        public IConnectiveRoom.State CurrentState() =>
            connectiveRoom.CurrentState();

        public IRoom Room() =>
            connectiveRoom.Room();

        private async UniTask PrewarmAsync(CancellationToken token)
        {
            await ConnectToArchipelagoAsync(token);
            signFlow.StartListeningForConnectionStringAsync(newString => OnNewConnectionString(newString, token), token);
        }

        private async UniTask SendHeartbeatAsync(ConnectToRoomAsyncDelegate connectDelegate, CancellationToken token)
        {
            connectToRoomAsyncDelegate = connectDelegate;
            await UniTask.SwitchToMainThread(token);
            Vector3 position = characterObject.Position;
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();
            await signFlow.SendHeartbeatAsync(position, token);
        }

        private void OnNewConnectionString(string connectionString, CancellationToken token)
        {
            if (CurrentState() is IConnectiveRoom.State.Sleep) throw new InvalidOperationException("Room is not running");
            connectToRoomAsyncDelegate.EnsureNotNull("Connection delegate is not passed yet");
            connectToRoomAsyncDelegate!(connectionString, token).Forget();
        }

        private async UniTask ConnectToArchipelagoAsync(CancellationToken token)
        {
            string adapterUrl = await adapterAddresses.AdapterUrlAsync(aboutUrl, token);
            LightResult<string> welcomePeerId = await WelcomePeerIdAsync(adapterUrl, token);
            welcomePeerId.EnsureSuccess("Cannot authorize with current address and signature, peer id is invalid");
        }

        private async UniTask<LightResult<string>> WelcomePeerIdAsync(string adapterUrl, CancellationToken token)
        {
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();
            IWeb3Identity identity = web3IdentityCache.EnsuredIdentity();
            await signFlow.ConnectAsync(adapterUrl, token);
            string ethereumAddress = identity.Address;
            string messageForSign = await signFlow.MessageForSignAsync(ethereumAddress, token);
            string signedMessage = identity.Sign(messageForSign).ToJson();
            ReportHub.Log(ReportCategory.ARCHIPELAGO_REQUEST, $"Signed message: {signedMessage}");
            return await signFlow.WelcomePeerIdAsync(signedMessage, token);
        }
    }
}
