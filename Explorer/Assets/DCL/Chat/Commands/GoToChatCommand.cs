using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Threading;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Teleports the player within Genesis to a specific, random, or crowded position,
    /// or to a different world and position.
    ///
    /// Usage:
    ///     /goto *x,y*          — teleport to parcel
    ///     /goto random         — teleport to a random parcel
    ///     /goto crowd          — teleport to the most populated scene
    ///     /goto *world*        — teleport to a world
    ///     /goto *world/x,y*    — teleport to a world at specific parcel
    /// </summary>
    public class GoToChatCommand : IChatCommand
    {
        public string Command => "goto";
        public string Description => "<b>/goto <i><x,y | random | crowd | world | world/x,y></i></b>\n  Teleport inside of Genesis or World";

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
            parameters.Length == 1;

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            if (ChatParamUtils.IsPositionParameter(parameters[0], true))
                return await chatTeleporter.TeleportToParcelAsync(await GetPositionAsync(parameters[0], ct), false, ct);

            if (TryParseWorldWithPosition(parameters[0], out string world, out string position))
                return await chatTeleporter.TeleportToRealmAsync(world, ChatParamUtils.ParseRawPosition(position), ct);

            return await chatTeleporter.TeleportToRealmAsync(parameters[0], ct);
        }

        private static bool TryParseWorldWithPosition(string param, out string world, out string position)
        {
            int slashIndex = param.IndexOf('/');

            if (slashIndex > 0 && slashIndex < param.Length - 1)
            {
                world = param.Substring(0, slashIndex);
                position = param.Substring(slashIndex + 1);
                return ChatParamUtils.IsPositionParameter(position, false);
            }

            world = null;
            position = null;
            return false;
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
                                        .GetAsync(urlsSource.Url(DecentralandUrl.ArchipelagoHotScenes), ct, ReportCategory.BADGES)
                                        .CreateFromNewtonsoftJsonAsync<HotScene[]>();

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
