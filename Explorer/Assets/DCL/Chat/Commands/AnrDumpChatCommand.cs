#if UNITY_STANDALONE_WIN
using Cysharp.Threading.Tasks;
using DCL.Diagnostics.Sentry;
using RichTypes;
using System.Threading;

namespace DCL.Chat.Commands
{
    public class AnrDumpChatCommand : IChatCommand
    {
        public string Command => "anr-dump";
        public string Description => "<b>/anr-dump</b>\n  Collect and archive a process dump to the app directory";
        public bool DebugOnly => true;

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            Result<(string filePath, string zipPath)> result = ThreadsDumpUtility.CollectAndArchiveDumpInfoToAppDir();

            if (result.Success == false)
                return UniTask.FromResult($"Dump failed: {result.ErrorMessage}");

            return UniTask.FromResult($"Dump collected:\n  {result.Value.filePath}\n  {result.Value.zipPath}");
        }
    }
}
#endif
