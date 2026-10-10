using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Chat.Commands
{
    public interface IChatCommand
    {
        string Command { get; }

        string Description { get; }

        bool DebugOnly => false;

        bool ValidateParameters(string[] parameters) =>
            parameters.Length == 0;

        UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct);
    }

    public static class ChatCommandsUtils
    {
        // Constants that shared between several ChatCommands
        public static string COMMAND_GOTO = "goto";
    }
}
