using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Typing;
using DCL.Web3.Identities;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;
using Utility.Types;
using Debug = UnityEngine.Debug;

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
                new ArchipelagoSignedConnection(new WebSocketArchipelagoLiveConnection(memoryPool),
                        multiPool, memoryPool, web3IdentityCache),
                memoryPool,
                multiPool
            ),
            characterObject,
            currentAdapterAddress
        ) { }

        public ArchipelagoIslandRoom(
            IArchipelagoSignFlow signFlow,
            ICharacterObject characterObject,
            ICurrentAdapterAddress currentAdapterAddress
        )
        {
            this.signFlow = signFlow;
            this.characterObject = characterObject;
            this.currentAdapterAddress = currentAdapterAddress;
        }

        Stopwatch connectionStringStopwatch = new Stopwatch();

        protected override async UniTask PrewarmAsync(CancellationToken token)
        {
            await ConnectToArchipelagoAsync(token);
            connectionStringStopwatch.Start();
            signFlow.StartListeningForConnectionStringAsync(OnNewConnectionString, token).Forget();
        }

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            if (newConnectionString != null)
            {
                string connectionString = newConnectionString;
                newConnectionString = null;
                Debug.Log($"JUANI ARCHIPELAGO NEW CONNECTION STRING: {connectionString}");
                Stopwatch stopwatchRoom = Stopwatch.StartNew();
                await TryConnectToRoomAsync(connectionString, token);
                stopwatchRoom.Stop();
                Debug.Log($"JUANI ARCHIPELAGO CONNECTED TO ROOM END: {stopwatchRoom.ElapsedMilliseconds}");
            }
            else
                Debug.Log($"JUANI ARCHIPELAGO MISSING CONNECTION STRING: {newConnectionString}");


            await UniTask.SwitchToMainThread(token);
            Vector3 position = characterObject.Position;
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();


            Stopwatch stopwatch = Stopwatch.StartNew();
            var result = await signFlow.SendHeartbeatAsync(position, token);
            stopwatch.Stop();
            Debug.Log($"JUANI ARCHIPELAGO HEARTBEAT END: {stopwatch.ElapsedMilliseconds}");


            if (result.Success == false)
                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, $"Cannot send heartbeat, connection is closed: {result.ErrorMessage}");
        }

        private void OnNewConnectionString(string connectionString)
        {
            connectionStringStopwatch.Stop();
            Debug.Log($"JUANI RECEIVED NEW CONNECTION STRING {connectionStringStopwatch.ElapsedMilliseconds}");
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
