using Cysharp.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.Chat.Commands
{
    public interface IChatCommand
    {


        UniTask<string> ExecuteAsync(Match match, CancellationToken ct);
    }

    public static class ChatCommandsUtils
    {
        // Constants that shared between several ChatCommands
        public static string COMMAND_GOTO = "goto";
    }
}
