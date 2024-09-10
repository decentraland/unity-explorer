using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using PortableExperiences.Controller;
using System.Text.RegularExpressions;
using System.Threading;

namespace Global.Dynamic.ChatCommands
{
    public class KillPortableExperienceChatCommand : IChatCommand
    {
        private const string COMMAND_PX = "killpx";
        private const string ENS_SUFFIX = ".dcl.eth";

        public static readonly Regex REGEX = new ($@"^/({COMMAND_PX})\s+((?!-?\d+\s*,\s*-?\d+$).+?)(?:\s+(-?\d+)\s*,\s*(-?\d+))?$", RegexOptions.Compiled);

        private readonly IPortableExperiencesController portableExperiencesController;

        private string? pxName;

        public KillPortableExperienceChatCommand(IPortableExperiencesController portableExperiencesController)
        {
            this.portableExperiencesController = portableExperiencesController;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            pxName = match.Groups[2].Value;

            if (!pxName.EndsWith(ENS_SUFFIX))
                pxName += ENS_SUFFIX;


            var response = portableExperiencesController.UnloadPortableExperienceByEns(new ENS(pxName));

            bool isSuccess = response.status;

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            return isSuccess ? $"🟢 The Portable Experience {pxName} has been Killed" :
                $"🔴 Error. Could not Kill the Portable Experience {pxName}";
        }
    }
}
