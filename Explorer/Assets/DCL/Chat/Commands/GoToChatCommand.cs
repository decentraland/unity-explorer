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
    /// or to a different world and position.
    ///
    /// Usage:
    ///     /goto *x,y*          — teleport to parcel
    ///     /goto *x,y/name*     — teleport to parcel, landing at the named spawn point
    ///     /goto random         — teleport to a random parcel
    ///     /goto crowd          — teleport to the most populated scene
    ///     /goto *world*        — teleport to a world
    ///     /goto *world/x,y*    — teleport to a world at specific parcel
    /// </summary>
    public class GoToChatCommand : IChatCommand
    {
        public string Command => "goto";
        public string Description => "<b>/goto <i><x,y | x,y/spawn | random | crowd | world | world/x,y></i></b>\n  Teleport inside of Genesis or World";

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
                return await chatTeleporter.TeleportToParcelAsync(GetRandomParcel(), false, ct);

            if (target.IsCrowd)
                return await chatTeleporter.TeleportToParcelAsync(await FindCrowdAsync(ct), false, ct);

            if (target.World != null)
                return target.Parcel.HasValue
                    ? await chatTeleporter.TeleportToRealmAsync(target.World, target.Parcel.Value, ct)
                    : await chatTeleporter.TeleportToRealmAsync(target.World, ct);

            if (target.Parcel is { } parcel)
                return await chatTeleporter.TeleportToParcelAsync(parcel, false, ct, target.SpawnPoint);

            // Unreachable: ParseGotoTarget always sets World when no other form matched.
            throw new InvalidOperationException($"Unrecognized /goto target: '{parameters[0]}'");
        }

        private static Vector2Int GetRandomParcel() =>
            new (
                Random.Range(GenesisCityData.MIN_PARCEL.x, GenesisCityData.MAX_SQUARE_CITY_PARCEL.x),
                Random.Range(GenesisCityData.MIN_PARCEL.y, GenesisCityData.MAX_SQUARE_CITY_PARCEL.y));

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
