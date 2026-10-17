using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.ScenesDebug.ScenesConsistency.ChatTeleports
{
    public interface IChatTeleport
    {
        UniTask WaitReadyAsync();

        void GoTo(Vector2Int coordinate);
    }
}
