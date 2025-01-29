using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Chat.History;
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
        public string Command => "clear";
        public string Description => "<b>/clear</b>\n    Clear the chat";

        public ChatController ChatController { get; set; }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            ChatController?.Clear();
            return UniTask.FromResult(string.Empty);
        }
    }
}
