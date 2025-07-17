using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using PortableExperiences.Controller;
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

        public KillPortableExperienceChatCommand(IPortableExperiencesController portableExperiencesController)
        {
            this.portableExperiencesController = portableExperiencesController;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 1;

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.PORTABLE_EXPERIENCE_CHAT_COMMANDS))
                return "🔴 Error. Portable Experiences Chat Commands are disabled";

            string pxName = parameters[0];

            if (pxName.EndsWith(ENS_SUFFIX, StringComparison.OrdinalIgnoreCase) == false)
                pxName += ENS_SUFFIX;

            await UniTask.SwitchToMainThread(ct);

            var response = portableExperiencesController.UnloadPortableExperienceByEns(new ENS(pxName));

            bool isSuccess = response.status;

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            return isSuccess ? $"🟢 The Portable Experience {pxName} has been Killed" : $"🔴 Error. Could not Kill the Portable Experience {pxName}";
        }
    }
}
