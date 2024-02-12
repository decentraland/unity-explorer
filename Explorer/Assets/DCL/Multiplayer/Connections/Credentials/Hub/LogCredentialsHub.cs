using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Credentials.Hub
{
    public class LogCredentialsHub : ICredentialsHub
    {
        private readonly ICredentialsHub origin;
        private readonly Action<string> log;

        public LogCredentialsHub(ICredentialsHub origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public async UniTask<ICredentials> SceneRoomCredentials(Vector2Int parcelPosition, CancellationToken token)
        {
            log("request for scene credentials started");
            var result = await origin.SceneRoomCredentials(parcelPosition, token);
            log($"request for scene credentials finished: {result.ReadableString()}");
            return result;
        }

        public async UniTask<ICredentials> IslandRoomCredentials(CancellationToken token)
        {
            log("request for island credentials started");
            var result = await origin.IslandRoomCredentials(token);
            log($"request for island credentials finished: {result.ReadableString()}");
            return result;
        }
    }
}
