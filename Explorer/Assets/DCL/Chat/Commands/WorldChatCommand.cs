using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using System.Threading;
using DCL.Chat.History;

namespace DCL.Chat.Commands
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

        public UniTask<string> ExecuteCommandAsync(ChatChannel channel, string[] parameters, CancellationToken ct) =>
            parameters.Length == 1

                // Case: /world <world>
                ? chatTeleporter.TeleportToRealmAsync(parameters[0], null, ct)

                // Case: /world <world> <x,y>
                : chatTeleporter.TeleportToRealmAsync(parameters[0], ChatParamUtils.ParseRawPosition(parameters[1]), ct);
    }
}
