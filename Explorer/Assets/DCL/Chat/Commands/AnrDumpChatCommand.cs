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

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            Result<(string filePath, string zipPath)> result = default;
            {
                await using var _ = await global::Utility.Multithreading.ExecuteOnThreadPoolScope.NewScopeAsync();
                result = ThreadsDumpUtility.CollectAndArchiveDumpInfoToAppDir();
            }

            if (result.Success == false) return $"Dump failed: {result.ErrorMessage}";
            return $"Dump collected:\n  {result.Value.filePath}\n  {result.Value.zipPath}";
        }
    }
}
#endif
