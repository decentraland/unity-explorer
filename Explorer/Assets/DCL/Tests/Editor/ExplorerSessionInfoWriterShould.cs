using DCL.Web3.Accounts.Factory;
using DCL.Web3.Identities;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.IO;

namespace DCL.EditModeTests
{
    [TestFixture]
    public class ExplorerSessionInfoWriterShould
    {
        private const string SESSION_ID = "session-abc-123";
        private const string EXPLORER_VERSION = "1.2.3";

        private string tempDir = null!;
        private Web3AccountFactory accountFactory = null!;
        private PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer serializer = null!;
        private MemoryWeb3IdentityCache identityCache = null!;
        private ExplorerSessionInfoWriter? writer;

        private string ExpectedPath => Path.Combine(tempDir, $"session-info-{SESSION_ID}.json");

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), $"dcl-session-info-{Path.GetRandomFileName()}");
            Directory.CreateDirectory(tempDir);

            accountFactory = new Web3AccountFactory();
            serializer = new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer(accountFactory);
            identityCache = new MemoryWeb3IdentityCache();
        }

        [TearDown]
        public void TearDown()
        {
            writer?.Dispose();
            writer = null;
            identityCache.Dispose();

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        [Test]
        public void WriteFileWithExpectedNameAndShapeWhenIdentityChanges()
        {
            writer = new ExplorerSessionInfoWriter(identityCache, serializer, SESSION_ID, EXPLORER_VERSION, tempDir);

            var identity = new IWeb3Identity.Random(accountFactory);
            identityCache.Identity = identity;

            Assert.IsTrue(File.Exists(ExpectedPath), "session-info file was not written under the expected name");

            JObject root = JObject.Parse(File.ReadAllText(ExpectedPath));

            Assert.AreEqual(1, (int)root["version"]!);
            Assert.AreEqual(SESSION_ID, (string)root["session_id"]!);
            Assert.AreEqual((string)identity.Address, (string)root["wallet"]!);
            Assert.AreEqual(EXPLORER_VERSION, (string)root["explorer_version"]!);

            // The nested identity object matches the serializer the client persists identity with.
            JObject expectedIdentity = JObject.Parse(serializer.Serialize(identity));
            Assert.IsTrue(JToken.DeepEquals(expectedIdentity, root["identity"]), "identity object shape does not match the identity serializer output");
            Assert.IsNotNull(root["identity"]!["key"], "nested identity must carry the ephemeral key the launcher signs with");
            Assert.AreEqual(JTokenType.Array, root["identity"]!["ephemeralAuthChain"]!.Type);
        }

        [Test]
        public void NotWriteFileWhenSessionIdIsMissing()
        {
            writer = new ExplorerSessionInfoWriter(identityCache, serializer, string.Empty, EXPLORER_VERSION, tempDir);

            identityCache.Identity = new IWeb3Identity.Random(accountFactory);

            Assert.IsEmpty(Directory.GetFiles(tempDir), "no file should be written when --session_id is absent");
        }

        [Test]
        public void DeleteFileOnShutdown()
        {
            writer = new ExplorerSessionInfoWriter(identityCache, serializer, SESSION_ID, EXPLORER_VERSION, tempDir);
            identityCache.Identity = new IWeb3Identity.Random(accountFactory);
            Assert.IsTrue(File.Exists(ExpectedPath), "precondition: file must exist before shutdown");

            // The clean-shutdown cleanup candidate registered via ExitUtils invokes exactly this.
            writer.DeleteSessionInfoFile();

            Assert.IsFalse(File.Exists(ExpectedPath), "session-info file must be removed on clean shutdown");
        }

        [Test]
        public void DeleteFileOnLogout()
        {
            writer = new ExplorerSessionInfoWriter(identityCache, serializer, SESSION_ID, EXPLORER_VERSION, tempDir);
            identityCache.Identity = new IWeb3Identity.Random(accountFactory);
            Assert.IsTrue(File.Exists(ExpectedPath));

            identityCache.Clear();

            Assert.IsFalse(File.Exists(ExpectedPath), "session-info file must be removed on logout");
        }
    }
}
