using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.History;
using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    public class ClearChatCommand : IChatCommand
    {
        public static readonly Regex REGEX = new ($@"^/(clear).*", RegexOptions.Compiled);

        private readonly IChatHistory chatHistory;

        public event Action? Executed;

        public ClearChatCommand(IChatHistory chatHistory)
        {
            this.chatHistory = chatHistory;
        }

        public UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            chatHistory.Clear();
            Executed?.Invoke();
            return UniTask.FromResult(string.Empty);
        }
    }
}
