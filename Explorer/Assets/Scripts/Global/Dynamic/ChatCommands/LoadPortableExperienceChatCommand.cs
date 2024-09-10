using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
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

        public static readonly Regex REGEX = new ($@"^/({COMMAND_PX})\s+((?!-?\d+\s*,\s*-?\d+$).+?)(?:\s+(-?\d+)\s*,\s*(-?\d+))?$", RegexOptions.Compiled);

        private readonly IPortableExperiencesController portableExperiencesController;

        private string? pxName;

        public LoadPortableExperienceChatCommand(IPortableExperiencesController portableExperiencesController)
        {
            this.portableExperiencesController = portableExperiencesController;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            pxName = match.Groups[2].Value;

            if (!pxName.EndsWith(ENS_SUFFIX))
                    pxName += ENS_SUFFIX;

            await UniTask.SwitchToMainThread(ct);

            bool isSuccess = await portableExperiencesController.CreatePortableExperienceByEnsAsyncWithErrorHandling(new ENS(pxName), ct, true, true);

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            return isSuccess ? $"🟢 The Portable Experience {pxName} has started loading" :
                 $"🔴 Error. Could not load {pxName} as a Portable Experience";
        }
    }
}
