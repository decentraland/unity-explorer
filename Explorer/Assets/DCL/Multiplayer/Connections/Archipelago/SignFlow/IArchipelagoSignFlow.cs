using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Typing;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.SignFlow
{
    public interface IArchipelagoSignFlow
    {
        UniTask EnsureConnectedAsync(string adapterUrl, CancellationToken token);

        UniTask<string> MessageForSignAsync(string ethereumAddress, CancellationToken token);

        UniTask<LightResult<string>> WelcomePeerIdAsync(string signedMessageAuthChainJson, CancellationToken token);

        UniTask SendHeartbeatAsync(Vector3 playerPosition, CancellationToken token);

        UniTaskVoid StartListeningForConnectionStringAsync(Action<string> onNewConnectionString, CancellationToken token);
    }

    public static class ArchipelagoSignFlowExtensions
    {
        public static IArchipelagoSignFlow WithLog(this IArchipelagoSignFlow origin) =>
            new LogArchipelagoSignFlow(origin);
    }
}
