using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Typing;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class ArchipelagoIslandRoom : ConnectiveRoom
    {
        private readonly IArchipelagoSignFlow signFlow;
        private readonly ICharacterObject characterObject;

        private readonly ICurrentAdapterAddress currentAdapterAddress;

        private string? newConnectionString;

        public ArchipelagoIslandRoom(ICharacterObject characterObject, IWeb3IdentityCache web3IdentityCache,
            IMultiPool multiPool, IMemoryPool memoryPool, ICurrentAdapterAddress currentAdapterAddress) : this(

            // TODO Validate the following assumption
            // We cannot use ArrayPool<byte>.Shared since some operations might not be thread safe (like the handshake)
            // producing unexpected errors when sending the data through the websocket
            new LiveConnectionArchipelagoSignFlow(
                new ArchipelagoSignedConnection(new WebSocketArchipelagoLiveConnection(memoryPool), multiPool, memoryPool, web3IdentityCache)
                   .WithLog(), memoryPool, multiPool).WithLog(), characterObject, currentAdapterAddress) { }

        public ArchipelagoIslandRoom(
            IArchipelagoSignFlow signFlow,
            ICharacterObject characterObject,
            ICurrentAdapterAddress currentAdapterAddress
        ) : base()
        {
            this.signFlow = signFlow;
            this.characterObject = characterObject;
            this.currentAdapterAddress = currentAdapterAddress;
        }

        protected override async UniTask PrewarmAsync(CancellationToken token)
        {
            await ConnectToArchipelagoAsync(token);
            signFlow.StartListeningForConnectionStringAsync(OnNewConnectionString, token).Forget();
        }

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            if (newConnectionString != null)
            {
                string connectionString = newConnectionString;
                newConnectionString = null;

                await TryConnectToRoomAsync(connectionString, token);
            }

            await UniTask.SwitchToMainThread(token);
            Vector3 position = characterObject.Position;
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();

            var result = await signFlow.SendHeartbeatAsync(position, token);

            if (result.Success == false)
                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, $"Cannot send heartbeat, connection is closed: {result.ErrorMessage}");
        }

        private void OnNewConnectionString(string connectionString)
        {
            newConnectionString = connectionString;
        }

        private async UniTask ConnectToArchipelagoAsync(CancellationToken token)
        {
            string adapterUrl = currentAdapterAddress.AdapterUrl();
            Result welcomePeerId = await signFlow.ConnectAsync(adapterUrl, token);
            welcomePeerId.EnsureSuccess("Cannot authorize with current address and signature, peer id is invalid");
        }
    }
}
