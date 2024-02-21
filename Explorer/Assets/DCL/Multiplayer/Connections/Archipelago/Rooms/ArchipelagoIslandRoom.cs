using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Credentials;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Typing;
using DCL.Web3.Identities;
using DCL.WebRequests;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Rooms;
using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class ArchipelagoIslandRoom : IArchipelagoIslandRoom
    {
        private readonly IAdapterAddresses adapterAddresses;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IArchipelagoSignFlow signFlow;
        private readonly IMultiPool multiPool;
        private readonly ICharacterObject characterObject;

        private readonly string aboutUrl;
        private readonly TimeSpan heartbeatsInterval;
        private readonly InteriorRoom room = new ();

        private CancellationTokenSource? cancellationTokenSource;

        public ArchipelagoIslandRoom(ICharacterObject characterObject, IWebRequestController webRequestController, IWeb3IdentityCache web3IdentityCache, IMultiPool multiPool) : this(
            new RefinedAdapterAddresses(
                new WebRequestsAdapterAddresses(webRequestController)
            ),
            web3IdentityCache,
            new LiveConnectionArchipelagoSignFlow(
                new WebSocketArchipelagoLiveConnection(
                    new ClientWebSocket(),
                    new ArrayMemoryPool(ArrayPool<byte>.Shared!)
                ),
                new ArrayMemoryPool(ArrayPool<byte>.Shared!),
                multiPool
            ),
            multiPool,
            characterObject,
            "https://realm-provider.decentraland.zone/main/about"
        ) { }

        public ArchipelagoIslandRoom(
            IAdapterAddresses adapterAddresses,
            IWeb3IdentityCache web3IdentityCache,
            IArchipelagoSignFlow signFlow,
            IMultiPool multiPool,
            ICharacterObject characterObject,
            string aboutUrl
        ) : this(
            adapterAddresses,
            web3IdentityCache,
            signFlow,
            multiPool,
            characterObject,
            aboutUrl
          , TimeSpan.FromSeconds(1)
        ) { }

        public ArchipelagoIslandRoom(IAdapterAddresses adapterAddresses, IWeb3IdentityCache web3IdentityCache, IArchipelagoSignFlow signFlow, IMultiPool multiPool, ICharacterObject characterObject,
            string aboutUrl,
            TimeSpan heartbeatsInterval)
        {
            this.adapterAddresses = adapterAddresses;
            this.web3IdentityCache = web3IdentityCache;
            this.signFlow = signFlow;
            this.multiPool = multiPool;
            this.characterObject = characterObject;
            this.aboutUrl = aboutUrl;
            this.heartbeatsInterval = heartbeatsInterval;
        }

        public void Start()
        {
            if (IsRunning())
                throw new InvalidOperationException("Room is already running");

            RunAsync().Forget();
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }

        public bool IsRunning() =>
            cancellationTokenSource is { IsCancellationRequested: false };

        public IRoom Room()
        {
            if (IsRunning() == false)
                throw new InvalidOperationException("Room is not running");

            return room;
        }

        private CancellationToken CancellationToken()
        {
            Stop();
            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private async UniTask RunAsync()
        {
            var token = CancellationToken();
            await ConnectToArchipelagoAsync(token);
            signFlow.StartListeningForConnectionStringAsync(OnNewConnectionString, token);
            await SendHeartbeatIntervalsAsync(token);
        }

        private async UniTask SendHeartbeatIntervalsAsync(CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                var position = characterObject.Position;
                await using var _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThread();
                await signFlow.SendHeartbeatAsync(position, token);
                await UniTask.Delay(heartbeatsInterval, cancellationToken: token);
            }
        }

        private void OnNewConnectionString(string connectionString)
        {
            if (IsRunning() == false) throw new Exception("Is not running");
            ConnectToRoomAsync(connectionString, cancellationTokenSource!.Token).Forget();
        }

        private async UniTask ConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            var newRoom = multiPool.Get<Room>();
            await newRoom.EnsuredConnectAsync(connectionString, multiPool, token);
            room.Assign(newRoom, out var previous);
            multiPool.TryRelease(previous);
        }

        private async UniTask ConnectToArchipelagoAsync(CancellationToken token)
        {
            await using var _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThread();
            string adapterUrl = await adapterAddresses.AdapterUrlAsync(aboutUrl, token);
            var welcomePeerId = await WelcomePeerIdAsync(adapterUrl, token);
            welcomePeerId.EnsureSuccess("Cannot authorize with current address and signature, peer id is invalid");
        }

        private async UniTask<LightResult<string>> WelcomePeerIdAsync(string adapterUrl, CancellationToken token)
        {
            var identity = web3IdentityCache.EnsuredIdentity();
            await signFlow.ConnectAsync(adapterUrl, token);
            string ethereumAddress = identity.Address;
            string messageForSign = await signFlow.MessageForSignAsync(ethereumAddress, token);
            string signedMessage = identity.Sign(messageForSign).ToJson();
            ReportHub.Log(ReportCategory.ARCHIPELAGO_REQUEST, $"Signed message: {signedMessage}");
            return await signFlow.WelcomePeerIdAsync(signedMessage, token);
        }
    }
}
