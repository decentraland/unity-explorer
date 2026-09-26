using Cysharp.Threading.Tasks;
using System.Globalization;
using System.Threading;

namespace DCL.Chat.Commands
{
    public class AnrSimulateChatCommand : IChatCommand
    {
        private const int DEFAULT_FREEZE_MS = 10_000;

        public string Command => "anr-simulate";
        public string Description => "<b>/anr-simulate <i>[ms]</i></b>\n  Freeze the main thread to trigger ANR detection";
        public bool DebugOnly => true;

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 0 || (parameters.Length == 1 && int.TryParse(parameters[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _));

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            int freezeMs = DEFAULT_FREEZE_MS;

            if (parameters.Length == 1)
                int.TryParse(parameters[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out freezeMs);

#if !UNITY_WEBGL
            Thread.Sleep(freezeMs); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
#endif

            return UniTask.FromResult($"Main thread was frozen for {freezeMs} ms.");
        }
    }
}
