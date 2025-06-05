using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Teleports the player within Genesis to a specific, random, or crowded position,
    /// or to a different realm and position.
    ///
    /// Usage:
    ///     /goto *realm*
    ///     /goto *realm* *x,y*
    ///     /goto *x,y | random | crowd*
    /// </summary>
    public class GoToChatCommand : IChatCommand
    {
        public string Command => "goto";
        public string Description => "<b>/goto <i><x,y | random | crowd></i></b>\n  Teleport inside of Genesis";

        private readonly ChatTeleporter chatTeleporter;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        public GoToChatCommand(ChatTeleporter chatTeleporter, IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource)
        {
            this.chatTeleporter = chatTeleporter;
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length == 1 || // /goto <realm> OR /goto <x,y | random | crowd>
            (parameters.Length == 2 && ChatParamUtils.IsPositionParameter(parameters[1], false)); // /goto <realm> <x,y>

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (parameters.Length == 1)
            {
                if (ChatParamUtils.IsPositionParameter(parameters[0], true))
                {
                    // Case: /goto <x,y | random | crowd>
                    return await chatTeleporter.TeleportToParcelAsync(await GetPositionAsync(parameters[0], ct), false, ct);
                }

                // LEGACY Case: /goto <realm>
                return await chatTeleporter.TeleportToRealmAsync(parameters[0], null, ct);
            }

            // LEGACY Case: /goto <realm> <x,y>
            return await chatTeleporter.TeleportToRealmAsync(parameters[0], await GetPositionAsync(parameters[1], ct), ct);
        }

        private UniTask<Vector2Int> GetPositionAsync(string positionParameter, CancellationToken ct)
        {
            return positionParameter switch
                   {
                       ChatParamUtils.PARAMETER_RANDOM => UniTask.FromResult(new Vector2Int(
                           Random.Range(GenesisCityData.MIN_PARCEL.x, GenesisCityData.MAX_SQUARE_CITY_PARCEL.x),
                           Random.Range(GenesisCityData.MIN_PARCEL.y, GenesisCityData.MAX_SQUARE_CITY_PARCEL.y))
                       ),
                       ChatParamUtils.PARAMETER_CROWD => FindCrowdAsync(ct),
                       _ => UniTask.FromResult(ChatParamUtils.ParseRawPosition(positionParameter))
                   };
        }

        private async UniTask<Vector2Int> FindCrowdAsync(CancellationToken ct)
        {
            HotScene[] hotScenes = await webRequestController
                                        .GetAsync(urlsSource.Url(DecentralandUrl.ArchipelagoHotScenes), ReportCategory.BADGES)
                                        .CreateFromNewtonsoftJsonAsync<HotScene[]>(ct);

            var topScene = hotScenes[0];

            return new Vector2Int(topScene.baseCoords[0], topScene.baseCoords[1]);
        }

        private struct HotScene
        {
            public string name;
            public int[] baseCoords;
        }
    }
}
