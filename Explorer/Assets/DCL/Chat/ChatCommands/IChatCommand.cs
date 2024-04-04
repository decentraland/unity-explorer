using Cysharp.Threading.Tasks;

namespace DCL.Chat.ChatCommands
{
    internal interface IChatCommand
    {
        UniTask<string> ExecuteAsync();
    }
}
