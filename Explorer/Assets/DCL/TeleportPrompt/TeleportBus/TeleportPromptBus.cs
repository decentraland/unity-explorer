using System;
using UnityEngine;

namespace DCL.TeleportPrompt
{
    /// <summary>
    ///     Shared event seam between the teleport prompt and the features that react to a confirmed teleport
    ///     (e.g. chat sends the "/goto" command). Lives in DCL.SharedAPI.Events so consumers don't need
    ///     to reference each other directly.
    /// </summary>
    public class TeleportPromptBus
    {
        public event Action<Vector2Int>? TeleportApproved;

        public void ApproveTeleport(Vector2Int coords) =>
            TeleportApproved?.Invoke(coords);
    }
}
