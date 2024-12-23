using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Threading;

namespace Global.Dynamic.ChatCommands
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
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        public GoToLocalChatCommand(ChatTeleporter chatTeleporter)
        {
            this.chatTeleporter = chatTeleporter;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 1 && ChatParamUtils.IsPositionParameter(parameters[0], false);

        public UniTask<string> ExecuteCommand(string[] parameters, CancellationToken ct) =>
            chatTeleporter.TeleportToParcel(ChatParamUtils.ParseRawPosition(parameters[3]), true, ct);
    }
}
