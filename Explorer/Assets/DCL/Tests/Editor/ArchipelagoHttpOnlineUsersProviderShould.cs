using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connectivity;
using DCL.WebRequests;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     <c>/comms/peers?id=</c> searches every realm, so one request answers for every friend and the world a
    ///     friend is in comes back with them. Nothing may reach a worlds-content-server
    ///     <c>/wallet/:wallet/connected-world</c> url any more - that per-friend lookup is gone.
    /// </summary>
    [TestFixture]
    public class ArchipelagoHttpOnlineUsersProviderShould
    {
        private const string BASE_URL = "https://archipelago-ea-stats.example.com/comms/peers";
        private const string FRIEND_IN_A_WORLD = "0x0000000000000000000000000000000000000003";
        private const string FRIEND_IN_GENESIS = "0x0000000000000000000000000000000000000001";
        private const string OFFLINE_FRIEND = "0x0000000000000000000000000000000000000009";

        private const string ALL_REALMS_RESPONSE = "{\"ok\":true,\"peers\":["
                                                   + "{\"address\":\"" + FRIEND_IN_GENESIS + "\",\"position\":[8,0,16],\"realm\":\"main\"},"
                                                   + "{\"address\":\"" + FRIEND_IN_A_WORLD + "\",\"position\":[8,0,8],\"realm\":\"cozyfarm.dcl.eth\"}"
                                                   + "]}";

        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new OnlinePlayersJsonDtoConverter() } };

        private IWebRequestController webRequestController = null!;
        private ArchipelagoHttpOnlineUsersProvider provider = null!;
        private List<string> requestedUrls = null!;

        [SetUp]
        public void SetUp()
        {
            requestedUrls = new List<string>();

            // The request is answered with the peers the real converter reads out of an all-realms response body
            List<OnlineUserData> peers = JsonConvert.DeserializeObject<List<OnlineUserData>>(ALL_REALMS_RESPONSE, SERIALIZER_SETTINGS)!;

            webRequestController = Substitute.For<IWebRequestController>();

            webRequestController
               .SendAsync<GenericGetRequest, GenericGetArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<List<OnlineUserData>, GenericGetRequest>, List<OnlineUserData>>(
                    Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                    Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<List<OnlineUserData>, GenericGetRequest>>())
               .Returns(call =>
                {
                    requestedUrls.Add(call.Arg<RequestEnvelope<GenericGetRequest, GenericGetArguments>>().CommonArguments.URL.Value);
                    return UniTask.FromResult(peers);
                });

            provider = new ArchipelagoHttpOnlineUsersProvider(webRequestController, URLAddress.FromString(BASE_URL));
        }

        [Test]
        public async Task AskForEveryFriendInOneRequest()
        {
            await provider.GetAsync(new[] { FRIEND_IN_A_WORLD, FRIEND_IN_GENESIS, OFFLINE_FRIEND }, CancellationToken.None);

            Assert.AreEqual(1, requestedUrls.Count, string.Join(", ", requestedUrls));

            Assert.AreEqual($"{BASE_URL}?id={FRIEND_IN_A_WORLD}&id={FRIEND_IN_GENESIS}&id={OFFLINE_FRIEND}", requestedUrls[0]);
        }

        [Test]
        public async Task NeverAskAWorldsServerWhereAFriendIs()
        {
            await provider.GetAsync(new[] { FRIEND_IN_A_WORLD, FRIEND_IN_GENESIS, OFFLINE_FRIEND }, CancellationToken.None);

            foreach (string url in requestedUrls)
                Assert.IsFalse(url.Contains("connected-world"), url);
        }

        [Test]
        public async Task ReportTheWorldOfAFriendFromThatOneRequest()
        {
            IReadOnlyCollection<OnlineUserData> onlineFriends = await provider.GetAsync(new[] { FRIEND_IN_A_WORLD, FRIEND_IN_GENESIS, OFFLINE_FRIEND }, CancellationToken.None);

            Assert.AreEqual(2, onlineFriends.Count);

            var worldsByFriend = new Dictionary<string, string?>();

            foreach (OnlineUserData friend in onlineFriends)
                worldsByFriend[friend.avatarId] = friend.worldName;

            Assert.AreEqual("cozyfarm.dcl.eth", worldsByFriend[FRIEND_IN_A_WORLD]);
            Assert.IsTrue(string.IsNullOrEmpty(worldsByFriend[FRIEND_IN_GENESIS]), worldsByFriend[FRIEND_IN_GENESIS]);
            Assert.IsFalse(worldsByFriend.ContainsKey(OFFLINE_FRIEND));
        }
    }
}
