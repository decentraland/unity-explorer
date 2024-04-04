using Cysharp.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DCL.Chat.ChatCommands
{
    internal interface IChatCommand
    {
        // Constants that shared between several ChatCommands
        const string COMMAND_GOTO = "goto";

        UniTask<string> ExecuteAsync();

        void Set(Match match);
    }
}
