using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.FeatureFlags;
using PortableExperiences.Controller;
using System.Text.RegularExpressions;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    public class KillPortableExperienceChatCommand : IChatCommand
    {
        private const string COMMAND_PX = "killpx";
        private const string ENS_SUFFIX = ".dcl.eth";

        private const string NAME_PATTERN = @"\s+(?<name>\w+)";
        private static readonly string COMMAND_PATTERN = $"^/(?<command>{Regex.Escape(COMMAND_PX)})";
        private static readonly string OPTIONAL_SUFFIX_PATTERN = $"(?<suffix>{Regex.Escape(ENS_SUFFIX)})?";

        public static readonly Regex REGEX = new($"{COMMAND_PATTERN}{NAME_PATTERN}{OPTIONAL_SUFFIX_PATTERN}$", RegexOptions.Compiled);

        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly FeatureFlagsCache featureFlagsCache;

        private string? pxName;

        public KillPortableExperienceChatCommand(IPortableExperiencesController portableExperiencesController, FeatureFlagsCache featureFlagsCache)
        {
            this.portableExperiencesController = portableExperiencesController;
            this.featureFlagsCache = featureFlagsCache;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            if (!featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.PORTABLE_EXPERIENCE_CHAT_COMMANDS))
                return "🔴 Error. Portable Experiences Chat Commands are disabled";

            pxName = match.Groups["name"].Value + ENS_SUFFIX;

            await UniTask.SwitchToMainThread(ct);

            var response = portableExperiencesController.UnloadPortableExperienceByEns(new ENS(pxName));

            bool isSuccess = response.status;

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            return isSuccess ? $"🟢 The Portable Experience {pxName} has been Killed" :
                $"🔴 Error. Could not Kill the Portable Experience {pxName}";
        }
    }
}
