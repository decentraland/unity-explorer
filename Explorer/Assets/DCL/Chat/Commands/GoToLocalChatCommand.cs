#nullable enable
using Cysharp.Threading.Tasks;
using System.Threading;
using DCL.Chat.History;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Teleports the player to a specific position inside the current realm.
    ///
    /// Usage:
    ///     /goto-local *x,y*
    /// </summary>
    public class GoToLocalChatCommand : IChatCommand
    {
        public string Command => "goto-local";
        public string Description => "<b>/goto-local <i><x,y></i></b>\n  Teleport inside of the current realm";

        private readonly ChatTeleporter chatTeleporter;

        public GoToLocalChatCommand(ChatTeleporter chatTeleporter)
        {
            this.chatTeleporter = chatTeleporter;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 1 && ChatParamUtils.IsPositionParameter(parameters[0], false);

        public UniTask<string> ExecuteCommandAsync(ChatChannel channel, string[] parameters, CancellationToken ct) =>
            chatTeleporter.TeleportToParcelAsync(ChatParamUtils.ParseRawPosition(parameters[0]), true, ct);
    }
}
