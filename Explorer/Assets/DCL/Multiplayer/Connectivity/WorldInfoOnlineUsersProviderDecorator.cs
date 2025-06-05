using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DCL.Multiplayer.Connectivity
{
    public class WorldInfoOnlineUsersProviderDecorator : IOnlineUsersProvider
    {
        private const string USER_ID_FIELD = "[USER-ID]";
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new OnlinePlayerInWorldJsonDtoConverter() } };

        private readonly IOnlineUsersProvider baseProvider;
        private readonly IWebRequestController webRequestController;
        private readonly Uri baseUrlWorlds;
        private readonly URLBuilder urlBuilder = new ();

        public WorldInfoOnlineUsersProviderDecorator(
            IOnlineUsersProvider baseProvider,
            IWebRequestController webRequestController,
            Uri baseUrlWorlds)
        {
            this.baseProvider = baseProvider;
            this.webRequestController = webRequestController;
            this.baseUrlWorlds = baseUrlWorlds;
        }

        //Using only base one as world user provider doesn't support a multiple wallet request
        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(CancellationToken ct) =>
            await baseProvider.GetAsync(ct);

        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(IEnumerable<string> userIds, CancellationToken ct)
        {
            var alreadyReturnedIds = new HashSet<string>();

            // First get the basic online users data from archipelago
            var onlineUsers = (await baseProvider.GetAsync(userIds, ct)).ToList();

            for (var i = 0; i < onlineUsers.Count; i++)
                alreadyReturnedIds.Add(onlineUsers[i].avatarId);

            // Then enhance with world online users information
            foreach (string userId in userIds)
            {
                //Skips requests for already returned wallet Ids, as they cannot be both in genesis city and in a world
                if (alreadyReturnedIds.Contains(userId))
                    continue;

                urlBuilder.Clear();
                urlBuilder.AppendDomain(URLDomain.FromString(baseUrlWorlds.OriginalString.Replace(USER_ID_FIELD, userId)));

                OnlineUserData worldUserData = await webRequestController.GetAsync(urlBuilder.Build(), ReportCategory.MULTIPLAYER)
                                                                         .CreateFromNewtonsoftJsonAsync<OnlineUserData>(ct, serializerSettings: SERIALIZER_SETTINGS)
                                                                         .SuppressExceptionWithFallbackAsync(default(OnlineUserData), ignoreTheseErrorCodesOnly: WebRequestUtils.IGNORE_NOT_FOUND);

                if (!string.IsNullOrEmpty(worldUserData.worldName))
                    onlineUsers.Add(worldUserData);
            }

            return onlineUsers;
        }
    }
}
