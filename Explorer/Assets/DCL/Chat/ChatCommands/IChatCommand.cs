using Cysharp.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DCL.Chat.ChatCommands
{
    internal interface IChatCommand
    {
        UniTask<string> ExecuteAsync();

        void Set(Match match);
    }
}
