using Cysharp.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.Chat
{
    public interface IChatCommand
    {
        // Constants that shared between several ChatCommands
        const string COMMAND_GOTO = "goto";
        const string PARAM_SOLO = "solo";
        const string PARAM_ONLY = "only";

        UniTask<string> ExecuteAsync(Match match, CancellationToken ct);
    }
}
