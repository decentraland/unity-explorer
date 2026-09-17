using DCL.Prefs;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;

namespace DCL.Web3.Tests
{
    [TestFixture]
    public class ProxyIdentityCacheShould
    {
        private const string STORED_JSON = "stored-identity";

        private IWeb3Identity identity = null!;
        private PlayerPrefsIdentityProvider.IWeb3IdentityJsonSerializer serializer = null!;
        private ProxyIdentityCache cache = null!;

        [SetUp]
        public void SetUp()
        {
            // Inject InMemoryDCLPlayerPrefs via reflection (established test pattern)
            SetPrefs(new InMemoryDCLPlayerPrefs());

            identity = Substitute.For<IWeb3Identity>();
            identity.IsExpired.Returns(false);

            serializer = Substitute.For<PlayerPrefsIdentityProvider.IWeb3IdentityJsonSerializer>();
            serializer.Serialize(identity).Returns(STORED_JSON);
            serializer.Deserialize(STORED_JSON).Returns(identity);

            cache = NewCache();
            cache.Identity = identity;
        }

        [TearDown]
        public void TearDown()
        {
            cache.Dispose();
            SetPrefs(null);
        }

        [Test]
        public void RestoreTheStoredIdentityWhenMemoryIsEmpty()
        {
            // Arrange
            ProxyIdentityCache freshCache = NewCache();

            // Act
            IWeb3Identity? restored = freshCache.Identity;

            // Assert
            Assert.That(restored, Is.SameAs(identity));
        }

        [Test]
        public void StayClearedWhenAClearedHandlerReadsTheIdentity()
        {
            // Arrange
            IWeb3Identity? readDuringClear = identity;
            cache.OnIdentityCleared += () => readDuringClear = cache.Identity;

            // Act
            cache.Clear();

            // Assert
            Assert.That(readDuringClear, Is.Null);
            Assert.That(cache.Identity, Is.Null);
            Assert.That(DCLPlayerPrefs.HasKey(DCLPrefKeys.WEB3_IDENTITY), Is.False);
        }

        private ProxyIdentityCache NewCache() =>
            new (new MemoryWeb3IdentityCache(), new PlayerPrefsIdentityProvider(serializer, EthereumNetwork.Mainnet));

        private static void SetPrefs(IDCLPrefs? prefs)
        {
            FieldInfo field = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;
            field.SetValue(null, prefs);
        }
    }
}
