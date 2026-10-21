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
    ///     /goto *x,y/name*     — teleport to parcel, landing at the named spawn point
    ///     /goto random         — teleport to a random parcel
    ///     /goto crowd          — teleport to the most populated scene
    ///     /goto *world*        — teleport to a world
    ///     /goto *world/x,y*    — teleport to a world at specific parcel
    ///     /goto *world/name*   — teleport to a world, landing at the named spawn point
    ///     /goto *world/x,y/name* — teleport to a world at specific parcel, landing at the named spawn point
    /// </summary>
    public class GoToChatCommand : IChatCommand
    {
        public string Command => "goto";
        public string Description => "<b>/goto <i><x,y | x,y/spawn | random | crowd | world | world/x,y | world/spawn | world/x,y/spawn></i></b>\n  Teleport inside of Genesis or World";

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
            GotoTarget target = ChatParamUtils.ParseGotoTarget(parameters[0]);

            if (target.IsRandom)
                return await chatTeleporter.TeleportToParcelAsync(RandomParcel(), false, ct);

            if (target.IsCrowd)
                return await chatTeleporter.TeleportToParcelAsync(await FindCrowdAsync(webRequestController, urlsSource, ct), false, ct);

            if (target.World != null)
                return target.Parcel.HasValue
                    ? await chatTeleporter.TeleportToRealmAsync(target.World, target.Parcel.Value, ct, target.SpawnPoint)
                    : await chatTeleporter.TeleportToRealmAsync(target.World, ct, target.SpawnPoint);

            if (target.Parcel is { } parcel)
                return await chatTeleporter.TeleportToParcelAsync(parcel, false, ct, target.SpawnPoint);

            return $"🔴 Invalid parameters, usage:\n{Description}";
        }

        /// <summary>A parcel of Genesis City picked at random, where <c>/goto random</c> goes.</summary>
        public static Vector2Int RandomParcel() =>
            new (
                Random.Range(GenesisCityData.MIN_PARCEL.x, GenesisCityData.MAX_SQUARE_CITY_PARCEL.x),
                Random.Range(GenesisCityData.MIN_PARCEL.y, GenesisCityData.MAX_SQUARE_CITY_PARCEL.y));

        /// <summary>The base parcel of the most populated scene, where <c>/goto crowd</c> goes.</summary>
        public static async UniTask<Vector2Int> FindCrowdAsync(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource, CancellationToken ct)
        {
            HotScene[] hotScenes = await webRequestController
                                        .GetAsync(urlsSource.Url(DecentralandUrl.ArchipelagoHotScenes), ct, ReportCategory.BADGES)
                                        .CreateFromNewtonsoftJsonAsync<HotScene[]>();

            var topScene = hotScenes[0];

            return new Vector2Int(topScene.baseCoords[0], topScene.baseCoords[1]);
        }

        // ReSharper disable InconsistentNaming
        // Archipelago hot-scenes API response - fields must match the JSON wire format.
        private struct HotScene
        {
            public string name;
            public int[] baseCoords;
        }
        // ReSharper restore InconsistentNaming
    }
}
