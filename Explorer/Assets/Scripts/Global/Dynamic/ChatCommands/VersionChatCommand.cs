using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using UnityEngine;

namespace Global.Dynamic.ChatCommands
{
    public class VersionChatCommand : IChatCommand
    {
        public string Command => "version";
        public string Description => "<b>/version</b>\n  Show the current version of the client";

        public bool ValidateParameters(string[] parameters)
        {
            return parameters.Length == 0;
        }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            return UniTask.FromResult(Application.version);
        }
    }
}