using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Teleports the player to a specific position inside the current realm.
    ///
    /// Usage:
    ///     /goto-local *x,y*        — teleport to parcel
    ///     /goto-local *x,y/name*   — teleport to parcel, landing at the named spawn point
    /// </summary>
    public class GoToLocalChatCommand : IChatCommand
    {
        public string Command => "goto-local";
        public string Description => "<b>/goto-local <i><x,y | x,y/spawn></i></b>\n  Teleport inside of the current realm";

        private readonly ChatTeleporter chatTeleporter;

        public GoToLocalChatCommand(ChatTeleporter chatTeleporter)
        {
            this.chatTeleporter = chatTeleporter;
        }

        public bool ValidateParameters(string[] parameters)
        {
            if (parameters.Length != 1)
                return false;

            GotoTarget target = ChatParamUtils.ParseGotoTarget(parameters[0]);
            return target.World == null && target.Parcel.HasValue;
        }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            GotoTarget target = ChatParamUtils.ParseGotoTarget(parameters[0]);
            return chatTeleporter.TeleportToParcelAsync(target.Parcel.Value, true, ct, target.SpawnPoint);
        }
    }
}
