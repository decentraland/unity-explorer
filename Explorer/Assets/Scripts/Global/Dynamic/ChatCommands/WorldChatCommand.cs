using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    /// <summary>
    /// Teleports the player to a different realm.
    ///
    /// Usage:
    ///     /world *world*
    ///     /world *world* *x,y*
    /// </summary>
    public class WorldChatCommand : IChatCommand
    {
        public string Command => "world";
        public string Description => "<b>/world <i><world> <x,y></i></b>\n  Teleport to a different realm";

        private readonly ChatTeleporter chatTeleporter;

        public WorldChatCommand(ChatTeleporter chatTeleporter)
        {
            this.chatTeleporter = chatTeleporter;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 1 || // Case: /world <world>
            (parameters.Length == 2 && ChatParamUtils.IsPositionParameter(parameters[1], false)); // Case: /world <world> <x,y>

        public UniTask<string> ExecuteCommand(string[] parameters, CancellationToken ct)
        {
            if (parameters.Length == 1)
            {
                // Case: /world <world>
                return chatTeleporter.TeleportToRealm(parameters[0], null, ct);
            }

            // Case: /world <world> <x,y>
            return chatTeleporter.TeleportToRealm(parameters[0], ChatParamUtils.ParseRawPosition(parameters[1]), ct);
        }
    }
}
