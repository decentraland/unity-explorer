using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Core;
using DCL.Diagnostics;
using System;
using System.Globalization;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Starts or stops the situational <see cref="StreamReactionsEmitter"/> at an arbitrary emit rate
    /// (reactions/sec, unclamped) to stress-test the particle reaction path. A rate of 0 or the 'stop'
    /// argument stops the stream. The control handle is attached by ChatPlugin once the reactions
    /// feature is created (this command is built earlier, in ChatContainer).
    /// </summary>
    public class StreamReactionsChatCommand : IChatCommand
    {
        private const string STOP_ARG = "stop";

        private IReactionStreamControl? streamControl;

        public string Command => "streamreactions";
        public string Description => "<b>/streamreactions <i>rate [sendBudget]</i></b>\n  Start the situational reaction stream at 'rate'/sec (unclamped). Use 0 or 'stop' to stop it";
        public bool DebugOnly => true;

        public void Attach(IReactionStreamControl control) =>
            streamControl = control;

        public void Detach() =>
            streamControl = null;

        public bool ValidateParameters(string[] parameters)
        {
            if (parameters.Length < 1 || parameters.Length > 2)
                return false;

            if (IsStopArg(parameters[0]))
                return parameters.Length == 1;

            if (!float.TryParse(parameters[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float rate) || rate < 0f)
                return false;

            return parameters.Length == 1
                   || (float.TryParse(parameters[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float budget) && budget >= 0f);
        }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (streamControl == null)
                return UniTask.FromResult("🔴 Reaction stream is not ready yet. Wait until chat has finished loading.");

            if (IsStopArg(parameters[0]))
            {
                streamControl.Stop();
                return UniTask.FromResult("Stopped the situational reaction stream.");
            }

            float rate = float.Parse(parameters[0], NumberStyles.Float, CultureInfo.InvariantCulture);

            if (rate <= 0f)
            {
                streamControl.Stop();
                return UniTask.FromResult("Stopped the situational reaction stream.");
            }

            float sendBudget = parameters.Length == 2
                ? float.Parse(parameters[1], NumberStyles.Float, CultureInfo.InvariantCulture)
                : rate;

            streamControl.Start(rate, sendBudget);

            string summary = $"Started the situational reaction stream at {rate.ToString(CultureInfo.InvariantCulture)}/sec (send budget {sendBudget.ToString(CultureInfo.InvariantCulture)}/sec).";
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[StreamReactions] {summary}");
            return UniTask.FromResult(summary);
        }

        private static bool IsStopArg(string value) =>
            string.Equals(value, STOP_ARG, StringComparison.OrdinalIgnoreCase);
    }
}
