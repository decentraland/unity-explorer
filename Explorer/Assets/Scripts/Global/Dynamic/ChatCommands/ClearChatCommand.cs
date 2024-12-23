using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.History;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    /// <summary>
    /// Clears the chat.
    ///
    /// Usage:
    ///    /clear
    /// </summary>
    public class ClearChatCommand : IChatCommand
    {
        public string Command => "clear";
        public string Description => "<b>/clear</b>\n    Clear the chat";

        private readonly IChatHistory chatHistory;

        public ClearChatCommand(IChatHistory chatHistory)
        {
            this.chatHistory = chatHistory;
        }

        public UniTask<string> ExecuteCommand(string[] parameters, CancellationToken ct)
        {
            chatHistory.Clear();
            return UniTask.FromResult(string.Empty);
        }
    }
}
