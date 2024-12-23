using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using Global.AppArgs;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    /// <summary>
    /// Prints a list of available chat commands and their descriptions.
    ///
    /// Usage:
    ///     /help
    /// </summary>
    public class HelpChatCommand : IChatCommand
    {
        public string Command => "help";

        public string Description => "<b>/help</b>\n  Shows this help message";
        public bool DebugOnly => false;

        private readonly IReadOnlyCollection<IChatCommand> commands;
        private readonly bool isDebug;

        public HelpChatCommand(IReadOnlyCollection<IChatCommand> commands, IAppArgs args)
        {
            isDebug = args.HasDebugFlag();
            this.commands = commands;
        }

        public UniTask<string> ExecuteCommand(string[] parameters, CancellationToken ct)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Available commands:\n");

            foreach (IChatCommand cmd in commands)
            {
                if (!isDebug && cmd.DebugOnly) continue;

                sb.AppendLine(cmd.Description);
            }

            return UniTask.FromResult(sb.ToString());
        }
    }
}
