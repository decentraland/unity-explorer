using Cysharp.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    ///     Companion to <see cref="AnrSimulateChatCommand" /> that stalls the main thread by burning CPU
    ///     instead of sleeping, so the captured minidump shows the main thread inside GameAssembly rather
    ///     than blocked in ntdll. Use the two together to compare how Sentry groups ANRs whose only
    ///     difference is the main-thread stack.
    /// </summary>
    public class AnrSpinChatCommand : IChatCommand
    {
        private const int DEFAULT_SPIN_MS = 10_000;

        public string Command => "anr-spin";
        public string Description => "<b>/anr-spin <i>[ms]</i></b>\n  Busy-spin the main thread to trigger ANR detection with a CPU-bound stack";
        public bool DebugOnly => true;

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 0 || (parameters.Length == 1 && int.TryParse(parameters[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _));

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            int spinMs = DEFAULT_SPIN_MS;

            if (parameters.Length == 1)
                int.TryParse(parameters[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out spinMs);

            SpinOuter(spinMs);

            return UniTask.FromResult($"Main thread spun for {spinMs} ms.");
        }

        private static void SpinOuter(int spinMs)
        {
            SpinInner(Stopwatch.StartNew(), spinMs);
        }

        private static void SpinInner(Stopwatch stopwatch, int spinMs)
        {
            // Reading the stopwatch keeps the loop from being optimised away
            while (stopwatch.ElapsedMilliseconds < spinMs) { }
        }
    }
}