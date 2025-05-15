#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.History;
using Global.Versioning;

namespace Global.Dynamic.ChatCommands
{
    public class VersionChatCommand : IChatCommand
    {
        private readonly DCLVersion dclVersion;

        public string Command => "version";
        public string Description => "<b>/version</b>\n  Show the current version of the client";

        public VersionChatCommand(DCLVersion dclVersion)
        {
            this.dclVersion = dclVersion;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 0;

        public UniTask<string> ExecuteCommandAsync(ChatChannel channel, string[] parameters, CancellationToken ct) =>
            UniTask.FromResult(dclVersion.Version);
    }
}
