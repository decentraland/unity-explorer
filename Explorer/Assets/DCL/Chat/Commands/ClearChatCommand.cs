using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Clears the chat.
    ///
    /// Usage:
    ///    /clear
    /// </summary>
    public class ClearChatCommand : IChatCommand
    {
        private readonly IChatCommandsBus chatCommandsBus;

        public ClearChatCommand(IChatCommandsBus chatCommandsBus)
        {
            this.chatCommandsBus = chatCommandsBus;
        }

        public string Command => "clear";
        public string Description => "<b>/clear</b>\n    Clear the chat";

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            chatCommandsBus.SendChatClearedNotification();
            return UniTask.FromResult(string.Empty);
        }
    }
}
