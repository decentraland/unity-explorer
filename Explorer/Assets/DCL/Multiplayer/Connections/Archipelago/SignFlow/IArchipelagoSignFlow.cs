using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Typing;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Credentials.Archipelago.SignFlow
{
    public interface IArchipelagoSignFlow
    {
        UniTask ConnectAsync(string adapterUrl, CancellationToken token);

        UniTask<string> MessageForSign(string ethereumAddress, CancellationToken token);

        UniTask<LightResult<string>> WelcomePeerId(string signedMessageAuthChainJson, CancellationToken token);

        UniTask SendHeartbeat(Vector3 playerPosition, CancellationToken token);

        UniTask StartListeningForConnectionString(Action<string> onNewConnectionString, CancellationToken token);
    }
}
