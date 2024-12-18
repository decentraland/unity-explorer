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
        public Regex Regex { get; } = new ("^/(help).*", RegexOptions.Compiled);
        public string Description => "<b>/help</b> - Shows this help message";

        private readonly IReadOnlyCollection<IChatCommand> commands;

        public HelpChatCommand(IReadOnlyCollection<IChatCommand> commands)
        {
            this.commands = commands;
        }

        public UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Available commands:\n");

            foreach (IChatCommand cmd in commands)
            {
                if (string.IsNullOrEmpty(cmd.Description)) continue;

                sb.AppendLine(cmd.Description);
            }

            return UniTask.FromResult(sb.ToString());
        }
    }
}
