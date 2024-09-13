using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Utilities.Extensions;
using PortableExperiences.Controller;
using System.Text.RegularExpressions;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    /// <summary>
    /// <example>
    /// Commands could be:
    ///     "/loadpx globalpx"
    ///     "/loadpx olavra.dcl.eth"
    ///This will load any world as a Global PX
    /// </example>
    /// </summary>
    public class LoadPortableExperienceChatCommand : IChatCommand
    {
        private const string COMMAND_PX = "loadpx";
        private const string ENS_SUFFIX = ".dcl.eth";

        private const string NAME_PATTERN = @"\s+(?<name>\w+)";
        private static readonly string COMMAND_PATTERN = $"^/(?<command>{Regex.Escape(COMMAND_PX)})";
        private static readonly string OPTIONAL_SUFFIX_PATTERN = $"(?<suffix>{Regex.Escape(ENS_SUFFIX)})?";

        public static readonly Regex REGEX = new($"{COMMAND_PATTERN}{NAME_PATTERN}{OPTIONAL_SUFFIX_PATTERN}$", RegexOptions.Compiled);

        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly FeatureFlagsCache featureFlagsCache;

        private string? pxName;

        public LoadPortableExperienceChatCommand(IPortableExperiencesController portableExperiencesController, FeatureFlagsCache featureFlagsCache)
        {
            this.portableExperiencesController = portableExperiencesController;
            this.featureFlagsCache = featureFlagsCache;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            if (!featureFlagsCache.Configuration.IsEnabled(FeatureFlagsConfiguration.GetFlag(FeatureFlags.PORTABLE_EXPERIENCE_CHAT_COMMANDS)))
                return "🔴 Error. Portable Experiences Chat Commands are disabled";

            //In this case as we are matching the suffix, either if it's present or not we need to append it to the string, so we can avoid the check and just add it always.
            pxName = match.Groups["name"].Value + ENS_SUFFIX;

            await UniTask.SwitchToMainThread(ct);

            var result = await portableExperiencesController.
                               CreatePortableExperienceByEnsAsync(new ENS(pxName), ct, true, true).
                               SuppressAnyExceptionWithFallback(new IPortableExperiencesController.SpawnResponse(), ReportCategory.PORTABLE_EXPERIENCE);

            bool isSuccess = !string.IsNullOrEmpty(result.ens);

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            return isSuccess ? $"🟢 The Portable Experience {pxName} has started loading" :
                 $"🔴 Error. Could not load {pxName} as a Portable Experience";
        }
    }
}
