using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Typing;
using DCL.Utility.Types;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.SignFlow
{
    public interface IArchipelagoSignFlow
    {
        UniTask<Result> ConnectAsync(string signedMessageAuthChainJson, CancellationToken token);

        UniTask<Result> SendHeartbeatAsync(Vector3 playerPosition, CancellationToken token);

        UniTaskVoid StartListeningForConnectionStringAsync(Action<string> onNewConnectionString, CancellationToken token);

        UniTask DisconnectAsync(CancellationToken token);
    }

    public static class ArchipelagoSignFlowExtensions
    {
        public static IArchipelagoSignFlow WithLog(this IArchipelagoSignFlow origin) =>
            new LogArchipelagoSignFlow(origin);
    }
}
