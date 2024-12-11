using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    public class HelpChatCommand : IChatCommand
    {
        public Regex Regex { get; } = new ($@"^/(help).*", RegexOptions.Compiled);
        public string Description => "/help - Shows this help message\n";

        private readonly List<IChatCommand> commands;

        public HelpChatCommand(List<IChatCommand> commands)
        {
            this.commands = commands;
        }

        public UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            var sb = new StringBuilder();

            sb.Append("Available commands:\n");

            foreach (IChatCommand cmd in commands) { sb.Append(cmd.Description); }

            return UniTask.FromResult(sb.ToString());
        }
    }
}
