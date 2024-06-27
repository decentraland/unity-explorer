using Cysharp.Threading.Tasks;
using System;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.Chat.Commands
{
    public interface IChatCommand
    {
        // Constants that shared between several ChatCommands
        const string COMMAND_GOTO = "goto";

        event Action? Executed;

        UniTask<string> ExecuteAsync(Match match, CancellationToken ct);
    }
}
