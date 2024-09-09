using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.SceneLifeCycle.Realm;
using PortableExperiences.Controller;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using static DCL.Chat.Commands.IChatCommand;

namespace Global.Dynamic.ChatCommands
{
    /// <summary>
    /// <example>
    /// Commands could be:
    ///     "/world genesis"
    ///     "/goto goerli"
    ///     "/world goerli 77,1"
    /// </example>
    /// </summary>
    public class LoadPortableExperienceChatCommand : IChatCommand
    {
        private const string COMMAND_PX = "loadpx";
        private const string ENS_SUFFIX = ".dcl.eth";


        public static readonly Regex REGEX = new ($@"^/({COMMAND_PX})\s+((?!-?\d+\s*,\s*-?\d+$).+?)(?:\s+(-?\d+)\s*,\s*(-?\d+))?$", RegexOptions.Compiled);
        private readonly URLDomain worldDomain = URLDomain.FromString(IRealmNavigator.WORLDS_DOMAIN);

        private readonly Dictionary<string, URLAddress> worldAddressesCaches = new ();
        private readonly IPortableExperiencesController portableExperiencesController;

        private string? pxName;
        private string? realmUrl;

        public LoadPortableExperienceChatCommand(IPortableExperiencesController portableExperiencesController)
        {
            this.portableExperiencesController = portableExperiencesController;
        }

        public async UniTask<string> ExecuteAsync(Match match, CancellationToken ct)
        {
            pxName = match.Groups[2].Value;

            if (!pxName.EndsWith(ENS_SUFFIX))
                    pxName += ENS_SUFFIX;


            bool isSuccess = await portableExperiencesController.CreatePortableExperienceByEnsAsyncWithErrorHandling(new ENS(pxName), ct, true, true);

            if (ct.IsCancellationRequested)
                return "🔴 Error. The operation was canceled!";

            return isSuccess ? $"🟢 The Portable Experience {pxName} has started loading" :
                 $"🔴 Error. Could not load {pxName} as a Portable Experience";
        }
    }
}
