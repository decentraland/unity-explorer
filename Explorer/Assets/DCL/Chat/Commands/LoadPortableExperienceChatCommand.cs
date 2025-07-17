using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Utilities.Extensions;
using PortableExperiences.Controller;
using System;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Load a Portable Experience.
    ///
    /// Usage:
    ///     /loadpx *name*
    ///
    /// <example>
    /// Commands could be:
    ///     "/loadpx globalpx"
    ///     "/loadpx olavra.dcl.eth"
    /// This will load any world as a Global PX
    /// </example>
    /// </summary>
    public class LoadPortableExperienceChatCommand : IChatCommand
    {
        private const string ENS_SUFFIX = ".dcl.eth";

        public string Command => "loadpx";
        public string Description => "<b>/loadpx <i><name></i></b>\n  Load a Portable Experience";
        public bool DebugOnly => true;

        private readonly IPortableExperiencesController portableExperiencesController;

        public LoadPortableExperienceChatCommand(IPortableExperiencesController portableExperiencesController)
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

            var result = await portableExperiencesController.CreatePortableExperienceByEnsAsync(new ENS(pxName), ct, true, true).SuppressAnyExceptionWithFallback(new IPortableExperiencesController.SpawnResponse(), ReportCategory.PORTABLE_EXPERIENCE);

            bool isSuccess = !string.IsNullOrEmpty(result.ens);

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            return isSuccess ? $"🟢 The Portable Experience {pxName} has started loading" : $"🔴 Error. Could not load {pxName} as a Portable Experience";
        }
    }
}
