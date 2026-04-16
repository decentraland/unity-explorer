using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.Commands;
using DCL.Utilities;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Debug chat command: toggles auto-generated reactions at a configurable rate for performance testing.
    /// Auto-enables DebugEnabled on start, restores on stop.
    /// Usage: /streamreactions [stop | emitRate sendBudget]
    /// </summary>
    public sealed class StreamReactionsChatCommand : IChatCommand
    {
        private const string STOP = "stop";

        private readonly ChatReactionsContainer container;

        private bool prevDebugEnabled;

        public string Command => "streamreactions";

        public string Description =>
            "<b>/streamreactions <i>[emitRate sendBudget]</i></b>\n" +
            "  Toggle reaction streaming for testing. 'stop' to stop.\n" +
            "  emitRate = local reactions/s, sendBudget = network reactions/s";

        public bool DebugOnly => true;

        public StreamReactionsChatCommand(ChatReactionsContainer container)
        {
            this.container = container;
        }

        public bool ValidateParameters(string[] parameters)
        {
            if (parameters.Length == 0) return true;
            if (parameters.Length == 1 && parameters[0] == STOP) return true;

            if (parameters.Length == 2)
            {
                return float.TryParse(parameters[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                    && float.TryParse(parameters[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            }

            return false;
        }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (!container.IsInitialized)
                return UniTask.FromResult("Reaction system not yet initialized.");

            StreamReactionsEmitter emitter = container.Result!.Value.StreamEmitter;

            if (parameters.Length == 1 || (parameters.Length == 0 && emitter.IsActive))
            {
                emitter.Stop();
                RestoreFlags();
                return UniTask.FromResult("Reaction streaming stopped.");
            }

            SaveAndEnableFlags();

            if (parameters.Length == 0)
            {
                emitter.StartWithDefaults();
                return UniTask.FromResult(
                    $"Streaming reactions (defaults): emit={emitter.EmitRate}/s, send={emitter.SendBudgetRate}/s");
            }

            float emitRate = float.Parse(parameters[0], CultureInfo.InvariantCulture);
            float sendBudget = float.Parse(parameters[1], CultureInfo.InvariantCulture);

            if (emitRate <= 0f || sendBudget <= 0f)
                return UniTask.FromResult("Both emitRate and sendBudget must be positive.");

            emitter.Start(emitRate, sendBudget);
            return UniTask.FromResult(
                $"Streaming reactions: emit={emitRate}/s, send={sendBudget}/s");
        }

        private void SaveAndEnableFlags()
        {
            var config = container.Config!;
            prevDebugEnabled = config.DebugEnabled;
            config.DebugEnabled = true;
        }

        private void RestoreFlags()
        {
            container.Config!.DebugEnabled = prevDebugEnabled;
        }
    }
}
