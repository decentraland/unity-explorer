using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.SignFlow
{
    public interface IArchipelagoSignFlow
    {
        UniTask<Result> ConnectAsync(string signedMessageAuthChainJson, CancellationToken token);

        /// <summary>
        ///     Invokes <paramref name="onNewIslandAssignment" /> with the island id and the connection
        ///     string of every assignment the server pushes.
        /// </summary>
        UniTaskVoid StartListeningForConnectionStringAsync(Action<string, string> onNewIslandAssignment, CancellationToken token);

        UniTask DisconnectAsync(CancellationToken token);
    }

    public static class ArchipelagoSignFlowExtensions
    {
        public static IArchipelagoSignFlow WithLog(this IArchipelagoSignFlow origin) =>
            new LogArchipelagoSignFlow(origin);
    }
}
