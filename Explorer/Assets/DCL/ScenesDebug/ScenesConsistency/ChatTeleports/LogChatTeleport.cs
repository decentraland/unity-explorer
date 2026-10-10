using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace DCL.ScenesDebug.ScenesConsistency.ChatTeleports
{
    public class LogChatTeleport : IChatTeleport
    {
        private readonly IChatTeleport origin;
        private readonly Action<string> log;

        public LogChatTeleport(IChatTeleport origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public async UniTask WaitReadyAsync()
        {
            log("Start wait for teleport");
            await origin.WaitReadyAsync();
            log("finish wait for teleport");
        }

        public void GoTo(Vector2Int coordinate)
        {
            log($"Teleporting to {coordinate}");
            origin.GoTo(coordinate);
            log($"Teleport finished {coordinate}");
        }
    }
}
