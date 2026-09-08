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
    ///     <c>/comms/peers?id=</c> searches every realm, so the ids asked for in one request all come back with
    ///     the realm they are in. Nothing may reach a worlds-content-server
    ///     <c>/wallet/:wallet/connected-world</c> url any more - that per-friend lookup is gone.
    ///     Every production call site (friend list, friend section, passport, both context menus) passes a
    ///     single-element buffer, so what the retired decorator cost was one extra request per jump-in click,
    ///     not one per friend. Batching is pinned here because the url composition is what the provider owns;
    ///     a caller that ever batches a real friend list has to respect the contract's cap of 200 ids per
    ///     request (201+ answers 400 <c>{"ok":false,"error":"too many ids (max 200)"}</c>).
    /// </summary>
    [TestFixture]
    public class ArchipelagoHttpOnlineUsersProviderShould
    {
        private const string BASE_URL = "https://archipelago-ea-stats.example.com/comms/peers";

        // The three ids the golden's own request line asks for, in that order
        private const string FRIEND_IN_A_WORLD = "0x0000000000000000000000000000000000000003";
        private const string FRIEND_IN_GENESIS = "0x0000000000000000000000000000000000000001";
        private const string OFFLINE_FRIEND = "0x0000000000000000000000000000000000000009";

        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new OnlinePlayersJsonDtoConverter() } };

        private IWebRequestController webRequestController = null!;
        private ArchipelagoHttpOnlineUsersProvider provider = null!;
        private List<string> requestedUrls = null!;

        [SetUp]
        public void SetUp()
        {
            requestedUrls = new List<string>();

            // The request is answered with the peers the real converter reads out of the golden all-realms
            // body itself - id, lastPing, parcel and realm included - so this fixture cannot drift from C2.
            List<OnlineUserData> peers = JsonConvert.DeserializeObject<List<OnlineUserData>>(
                OnlinePlayersJsonDtoConverterShould.GoldenPeersBody(), SERIALIZER_SETTINGS)!;

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
        public async Task AskForEveryRequestedIdInOneRequest()
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
