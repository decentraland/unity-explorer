using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using PortableExperiences.Controller;
using Runtime.Wearables;
using System;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Kills a portable experience.
    ///
    /// Usage:
    ///     /killpx *name*
    /// </summary>
    public class KillPortableExperienceChatCommand : IChatCommand
    {
        private const string ENS_SUFFIX = ".dcl.eth";

        public string Command => "killpx";
        public string Description => "<b>/killpx <i><name></i></b>\n  Kill a portable experience";
        public bool DebugOnly => true;

        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly SmartWearableCache smartWearableCache;

        public KillPortableExperienceChatCommand(IPortableExperiencesController portableExperiencesController, SmartWearableCache smartWearableCache)
        {
            this.portableExperiencesController = portableExperiencesController;
            this.smartWearableCache = smartWearableCache;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 1;

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (!FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.PORTABLE_EXPERIENCE_CHAT_COMMANDS))
                return "🔴 Error. Portable Experiences Chat Commands are disabled";

            await UniTask.SwitchToMainThread(ct);
            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            string portableExperienceId = parameters[0];

            // Try killing a px with the given ID as it is
            var response = portableExperiencesController.UnloadPortableExperienceById(portableExperienceId);

            // In case of failure, try appending the ENS suffix and retrying
            if (!response.status && !portableExperienceId.EndsWith(ENS_SUFFIX, StringComparison.OrdinalIgnoreCase))
            {
                portableExperienceId += ENS_SUFFIX;
                response = portableExperiencesController.UnloadPortableExperienceById(portableExperienceId);
            }

            if (response.status) smartWearableCache.KilledPortableExperiences.Add(portableExperienceId);

            return response.status ? $"🟢 The Portable Experience {portableExperienceId} has been Killed" : $"🔴 Error. Could not Kill the Portable Experience {portableExperienceId}";
        }
    }
}
