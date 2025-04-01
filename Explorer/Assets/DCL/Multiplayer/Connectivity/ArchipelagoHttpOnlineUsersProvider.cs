using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Connectivity
{
    public class ArchipelagoHttpOnlineUsersProvider : IOnlineUsersProvider
    {
        private const string USER_ID_FIELD = "[USER-ID]";
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new OnlinePlayersJsonDtoConverter(), new OnlinePlayerInWorldJsonDtoConverter() } };

        private readonly IWebRequestController webRequestController;
        private readonly URLAddress baseUrl;
        private readonly URLAddress baseUrlWorlds;
        private readonly URLBuilder urlBuilder = new ();

        public ArchipelagoHttpOnlineUsersProvider(
            IWebRequestController webRequestController,
            URLAddress baseUrl,
            URLAddress baseUrlWorlds)
        {
            this.webRequestController = webRequestController;
            this.baseUrl = baseUrl;
            this.baseUrlWorlds = baseUrlWorlds;
        }

        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(CancellationToken ct) =>
            await webRequestController.GetAsync(baseUrl, ct, ReportCategory.MULTIPLAYER)
                                      .CreateFromNewtonsoftJsonAsync<List<OnlineUserData>>(serializerSettings: SERIALIZER_SETTINGS);

        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(IEnumerable<string> userIds, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(URLDomain.FromString(baseUrl));

            foreach (string userId in userIds)
                urlBuilder.AppendParameter(new URLParameter("id", userId));

            List<OnlineUserData> onlineUsers = await webRequestController.GetAsync(urlBuilder.Build(), ct, ReportCategory.MULTIPLAYER)
                                                                                     .CreateFromNewtonsoftJsonAsync<List<OnlineUserData>>(serializerSettings: SERIALIZER_SETTINGS);

            foreach (string userId in userIds)
            {
                urlBuilder.Clear();
                urlBuilder.AppendDomain(URLDomain.FromString(baseUrlWorlds.Value.Replace(USER_ID_FIELD, userId)));

                onlineUsers.Add(await webRequestController.GetAsync(urlBuilder.Build(), ct, ReportCategory.MULTIPLAYER, ignoreErrorCodes: IWebRequestController.IGNORE_NOT_FOUND)
                                                          .CreateFromNewtonsoftJsonAsync<OnlineUserData>(serializerSettings: SERIALIZER_SETTINGS));
            }

            return onlineUsers;
        }
    }
}
