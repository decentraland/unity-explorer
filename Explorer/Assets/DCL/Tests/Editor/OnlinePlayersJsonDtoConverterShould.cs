using DCL.Multiplayer.Connectivity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     Every peer of an all-realms <c>/comms/peers?id=</c> response carries the realm it is in, so a friend's
    ///     world name is read from that one response. The golden body is the fixture pack's
    ///     <c>http/peers-by-id.json</c>, copied verbatim into TestResources.
    /// </summary>
    [TestFixture]
    public class OnlinePlayersJsonDtoConverterShould
    {
        private const string PEER_IN_A_WORLD = "0x0000000000000000000000000000000000000003";
        private const string PEER_IN_GENESIS = "0x0000000000000000000000000000000000000001";

        // A peer with a position and an optional realm field, so the absent-realm case is expressible too
        private const string ONE_PEER_TEMPLATE = "{{\"ok\":true,\"peers\":[{{\"address\":\"{0}\",\"position\":[8,0,8]{1}}}]}}";

        private static readonly string GOLDEN_PATH = $"{Application.dataPath}/../TestResources/iteration-2/http/peers-by-id.json";

        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new OnlinePlayersJsonDtoConverter() } };

        [Test]
        public void ReadTheWorldNameOfTheGoldenResponseFromTheRealm()
        {
            List<OnlineUserData> users = Deserialize(GoldenPeersBody());

            Assert.AreEqual(2, users.Count);

            OnlineUserData worldUser = UserOf(users, PEER_IN_A_WORLD);
            Assert.AreEqual("cozyfarm.dcl.eth", worldUser.worldName);
            Assert.IsTrue(worldUser.IsInWorld);
            Assert.AreEqual(new Vector3(8, 0, 8), worldUser.position);
        }

        [Test]
        public void LeaveAGenesisPeerWithoutAWorldName()
        {
            List<OnlineUserData> users = Deserialize(GoldenPeersBody());

            OnlineUserData genesisUser = UserOf(users, PEER_IN_GENESIS);
            Assert.IsTrue(string.IsNullOrEmpty(genesisUser.worldName), genesisUser.worldName);
            Assert.IsFalse(genesisUser.IsInWorld);
        }

        [TestCase("\"realm\":\"cozyfarm.dcl.eth\"", "cozyfarm.dcl.eth")]
        [TestCase("\"realm\":\"CozyFarm.DCL.ETH\"", "CozyFarm.DCL.ETH")] // the suffix matches either casing, the name is kept verbatim
        [TestCase("\"realm\":\"main\"", null)]
        [TestCase("\"realm\":\"sepolia\"", null)]
        [TestCase("\"realm\":\"cozyfarm.eth\"", null)] // an ens name that is not a world
        [TestCase("\"realm\":\"\"", null)]
        [TestCase("\"realm\":null", null)]
        [TestCase("", null)] // an endpoint that does not carry the realm at all
        public void NameAWorldOnlyForADclEthRealm(string realmField, string? expectedWorldName)
        {
            string peerField = realmField.Length == 0 ? string.Empty : "," + realmField;

            List<OnlineUserData> users = Deserialize(string.Format(ONE_PEER_TEMPLATE, PEER_IN_A_WORLD, peerField));

            Assert.AreEqual(1, users.Count);
            Assert.AreEqual(expectedWorldName, users[0].worldName);
            Assert.AreEqual(expectedWorldName != null, users[0].IsInWorld);
        }

        private static List<OnlineUserData> Deserialize(string json) =>
            JsonConvert.DeserializeObject<List<OnlineUserData>>(json, SERIALIZER_SETTINGS)!;

        /// <summary>
        ///     The golden file wraps the response, so only its <c>body</c> is what the converter reads.
        ///     <c>internal</c> so <c>ArchipelagoHttpOnlineUsersProviderShould</c> answers its stubbed request
        ///     with the very same bytes instead of a paraphrase that can drift from the contract.
        /// </summary>
        internal static string GoldenPeersBody()
        {
            var golden = JObject.Parse(File.ReadAllText(GOLDEN_PATH));
            return golden["body"]!.ToString(Formatting.None);
        }

        private static OnlineUserData UserOf(IReadOnlyList<OnlineUserData> users, string avatarId)
        {
            foreach (OnlineUserData user in users)
                if (user.avatarId == avatarId)
                    return user;

            Assert.Fail($"{avatarId} is not among the deserialized peers");
            return default(OnlineUserData);
        }
    }
}
