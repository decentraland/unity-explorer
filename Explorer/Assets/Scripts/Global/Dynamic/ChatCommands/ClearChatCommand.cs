using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.History;
using System.Text.RegularExpressions;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    public class ClearChatCommand : IChatCommand
    {
        public Regex Regex { get; } = new ($@"^/(clear).*", RegexOptions.Compiled);
        public string Description => "/clear - Clear the chat";

        private readonly IChatHistory chatHistory;

        public ClearChatCommand(IChatHistory chatHistory)
        {
            this.chatHistory = chatHistory;
        }

        public UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            chatHistory.Clear();
            return UniTask.FromResult(string.Empty);
        }
    }
}
