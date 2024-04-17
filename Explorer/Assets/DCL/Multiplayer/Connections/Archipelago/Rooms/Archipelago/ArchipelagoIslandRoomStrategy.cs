using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Typing;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using System;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    internal class ArchipelagoIslandRoomStrategy : IRealmRoomStrategy
    {
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IArchipelagoSignFlow signFlow;
        private readonly ICharacterObject characterObject;
        private readonly string currentAdapterAddress;

        public ConnectiveRoom ConnectiveRoom { get; }

        private ConnectToRoomAsyncDelegate? connectToRoomAsyncDelegate;

        public ArchipelagoIslandRoomStrategy(
            InteriorRoom sharedRoom,
            IWeb3IdentityCache web3IdentityCache,
            IArchipelagoSignFlow signFlow,
            ICharacterObject characterObject,
            string currentAdapterAddress
        )
        {
            this.web3IdentityCache = web3IdentityCache;
            this.signFlow = signFlow;
            this.characterObject = characterObject;
            this.currentAdapterAddress = currentAdapterAddress;

            ConnectiveRoom = new ConnectiveRoom(sharedRoom, PrewarmAsync, SendHeartbeatAsync);
        }

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

            try { await signFlow.SendHeartbeatAsync(position, token); }
            catch (ConnectionClosedException)
            {
                //ignore
                ReportHub.LogWarning(ReportCategory.ARCHIPELAGO_REQUEST, "Cannot send heartbeat, connection is closed");
            }
        }

        private void OnNewConnectionString(string connectionString, CancellationToken token)
        {
            if (ConnectiveRoom.CurrentState() is IConnectiveRoom.State.Stopped) throw new InvalidOperationException("Room is not running");
            connectToRoomAsyncDelegate.EnsureNotNull("Connection delegate is not passed yet");
            connectToRoomAsyncDelegate!(connectionString, token).Forget();
        }

        private async UniTask ConnectToArchipelagoAsync(CancellationToken token)
        {
            LightResult<string> welcomePeerId = await WelcomePeerIdAsync(currentAdapterAddress, token);
            welcomePeerId.EnsureSuccess("Cannot authorize with current address and signature, peer id is invalid");
        }

        private async UniTask<LightResult<string>> WelcomePeerIdAsync(string adapterUrl, CancellationToken token)
        {
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();
            IWeb3Identity identity = web3IdentityCache.EnsuredIdentity();
            await signFlow.EnsureConnectedAsync(adapterUrl, token);
            string ethereumAddress = identity.Address;
            string messageForSign = await signFlow.MessageForSignAsync(ethereumAddress, token);
            string signedMessage = identity.Sign(messageForSign).ToJson();
            ReportHub.Log(ReportCategory.ARCHIPELAGO_REQUEST, $"Signed message: {signedMessage}");
            return await signFlow.WelcomePeerIdAsync(signedMessage, token);
        }
    }
}
