using Cysharp.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.Chat
{
    public interface IChatCommand
    {
        // Constants that shared between several ChatCommands
        const string COMMAND_GOTO = "goto";

        UniTask<string> ExecuteAsync(Match match, CancellationToken ct);
    }
}
