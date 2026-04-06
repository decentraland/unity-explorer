using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Debug;
using DCL.Chat.Commands;
using DCL.Utilities;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Debug chat command: fakes reactions from all nearby avatars for solo performance testing.
    /// Activates world particles above real nearby avatars + UI lane injection + local player streaming.
    /// Auto-enables DebugEnabled and DynamicScalingEnabled on start, restores on stop.
    /// Usage: /fakereactions [stop]
    /// </summary>
    public sealed class FakeReactionsChatCommand : IChatCommand
    {
        private const string STOP = "stop";

        private readonly ObjectProxy<SituationalReactionDebugController> debugControllerProxy;
        private readonly ObjectProxy<ChatReactionsConfig> configProxy;

        private bool isActive;
        private bool prevDebugEnabled;
        private bool prevDynamicScaling;

        public string Command => "fakereactions";

        public string Description =>
            "<b>/fakereactions</b>\n" +
            "  Toggle fake reactions from all nearby avatars (solo perf test).\n" +
            "  'stop' to stop.";

        public bool DebugOnly => true;

        public FakeReactionsChatCommand(
            ObjectProxy<SituationalReactionDebugController> debugControllerProxy,
            ObjectProxy<ChatReactionsConfig> configProxy)
        {
            this.debugControllerProxy = debugControllerProxy;
            this.configProxy = configProxy;
        }

        public bool ValidateParameters(string[] parameters)
        {
            if (parameters.Length == 0) return true;
            if (parameters.Length == 1 && parameters[0] == STOP) return true;
            return false;
        }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (!debugControllerProxy.Configured)
                return UniTask.FromResult("Reaction system not yet initialized.");

            SituationalReactionDebugController controller = debugControllerProxy.Object!;

            // Stop
            if ((parameters.Length == 1 && parameters[0] == STOP) || (parameters.Length == 0 && isActive))
            {
                controller.EndDebugNearby();
                controller.EndDebugLocalStream();
                RestoreFlags();
                isActive = false;
                return UniTask.FromResult("Fake reactions stopped.");
            }

            // Start
            SaveAndEnableFlags();
            controller.BeginDebugNearby();
            controller.BeginDebugLocalStream();
            isActive = true;

            return UniTask.FromResult("Fake reactions active: nearby avatars (world + UI) + local player.");
        }

        private void SaveAndEnableFlags()
        {
            if (!configProxy.Configured) return;

            var config = configProxy.Object!;
            prevDebugEnabled = config.DebugEnabled;
            prevDynamicScaling = config.DynamicScalingEnabled;
            config.DebugEnabled = true;
            config.DynamicScalingEnabled = true;
        }

        private void RestoreFlags()
        {
            if (!configProxy.Configured) return;

            var config = configProxy.Object!;
            config.DebugEnabled = prevDebugEnabled;
            config.DynamicScalingEnabled = prevDynamicScaling;
        }
    }
}
