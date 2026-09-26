using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Teleports the player to a specific position inside the current realm.
    ///
    /// Usage:
    ///     /goto-local *x,y*        — teleport to parcel
    ///     /goto-local *x,y/name*   — teleport to parcel, landing at the named spawn point
    ///     /goto-local *name*       — teleport to the named spawn point of the current scene
    /// </summary>
    public class GoToLocalChatCommand : IChatCommand
    {
        public string Command => "goto-local";
        public string Description => "<b>/goto-local <i><x,y | x,y/spawn | spawn></i></b>\n  Teleport inside of the current realm";

        private readonly ChatTeleporter chatTeleporter;
        private readonly IScenesCache scenesCache;

        public GoToLocalChatCommand(ChatTeleporter chatTeleporter, IScenesCache scenesCache)
        {
            this.chatTeleporter = chatTeleporter;
            this.scenesCache = scenesCache;
        }

        public bool ValidateParameters(string[] parameters)
        {
            if (parameters.Length != 1)
                return false;

            GotoTarget target = ChatParamUtils.ParseGotoTarget(parameters[0]);

            if (target.World == null)
                return target.Parcel.HasValue;

            // A bare name (parsed as a world) targets a spawn point of the scene the player stands in
            return target.Parcel == null && target.SpawnPoint == null && ChatParamUtils.IsSpawnPointName(target.World);
        }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            GotoTarget target = ChatParamUtils.ParseGotoTarget(parameters[0]);

            return target.Parcel.HasValue
                ? chatTeleporter.TeleportToParcelAsync(target.Parcel.Value, true, ct, target.SpawnPoint)
                : chatTeleporter.TeleportToParcelAsync(scenesCache.CurrentParcel.Value, true, ct, target.World);
        }
    }
}
