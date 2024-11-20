using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.UsersMarker
{
    public class RemoteUsersRequestController
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new RemotePlayersJsonDtoConverter() } };

        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private string baseURL => decentralandUrlsSource.Url(DecentralandUrl.RemotePeers);
        private readonly IWebRequestController webRequestController;

        public RemoteUsersRequestController(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<List<RemotePlayersDTOs.RemotePlayerData>> RequestUsers(CancellationToken ct) =>
            await webRequestController.GetAsync(baseURL, ct, ReportCategory.UI)
                                      .CreateFromNewtonsoftJsonAsync<List<RemotePlayersDTOs.RemotePlayerData>>(serializerSettings: SERIALIZER_SETTINGS);
    }

}
