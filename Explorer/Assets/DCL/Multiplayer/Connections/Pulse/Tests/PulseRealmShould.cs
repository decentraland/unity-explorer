using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Utility.Types;
using ECS;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Multiplayer.Connections.Pulse.Tests
{
    /// <summary>
    ///     The local scene development realm key is a cross-repo contract (js-sdk-toolchain
    ///     <c>logic/lsd-realm.ts</c>, bevy-explorer) that nothing exchanges at runtime: a derivation that
    ///     drifts does not error, the peers just never see each other. These tests pin the exact strings,
    ///     including the worked examples published in the shared contract doc,
    ///     <c>docs/lsd-identity-and-pulse-realm.md</c>.
    /// </summary>
    [TestFixture]
    public class PulseRealmShould
    {
        private const string MACHINE_ID = "dev-box";
        private const string ENTITY_ID = "b64-L2hvbWUvZGV2L215LXNjZW5l";

        private IRealmData realmData = null!;
        private ILocalSceneEntityIdSource entityIdSource = null!;

        [SetUp]
        public void SetUp()
        {
            // Unresolved-realm paths report through ReportHub; that is the behaviour under test, not a failure.
            LogAssert.ignoreFailingMessages = true;
            realmData = Substitute.For<IRealmData>();
            entityIdSource = Substitute.For<ILocalSceneEntityIdSource>();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void FollowTheCurrentRealmLiveOutsideLocalSceneDevelopment()
        {
            // Arrange
            var currentRealm = "main";
            realmData.RealmName.Returns(_ => currentRealm);
            var realm = new PulseRealm(realmData);

            // Assert — read live, because a teleport must be visible to the very next message
            Assert.That(realm.Value, Is.EqualTo("main"));

            // Act
            currentRealm = "some-world.dcl.eth";

            // Assert
            Assert.That(realm.Value, Is.EqualTo("some-world.dcl.eth"));
        }

        [Test]
        public async Task NotResolveAnythingOutsideLocalSceneDevelopment()
        {
            // Arrange
            realmData.RealmName.Returns("main");
            var realm = new PulseRealm(realmData);

            // Act
            await realm.EnsureResolvedAsync(CancellationToken.None);

            // Assert
            Assert.That(realm.Value, Is.EqualTo("main"));
        }

        [Test]
        public void MatchTheRawKeyVectorFromTheSharedContract()
        {
            // Arrange
            string previewSceneId = PreviewSceneId("/home/dev/my-scene");

            // Act
            string realmKey = PulseRealm.RealmKeyFor(previewSceneId);

            // Assert
            Assert.That(realmKey, Is.EqualTo("lsd:b64-L2hvbWUvZGV2L215LXNjZW5lLWRldi1ib3g="));
        }

        [Test]
        public void MatchTheHashedKeyVectorFromTheSharedContract()
        {
            // Arrange
            string previewSceneId = PreviewSceneId("/home/dev/" + new string('a', 200));
            Assert.That(("lsd:" + previewSceneId).Length, Is.GreaterThan(PulseRealm.MAX_REALM_LENGTH));

            // Act
            string realmKey = PulseRealm.RealmKeyFor(previewSceneId);

            // Assert
            Assert.That(realmKey, Is.EqualTo("lsd:sha256:783635fb50eadaed0300d80104920bfc55894d5ad2ab69ab6b48c6ff1ddb9da5"));
        }

        [Test]
        public void KeepTheRawKeyAtTheLengthLimit()
        {
            // Arrange
            var previewSceneId = new string('x', PulseRealm.MAX_REALM_LENGTH - 4);

            // Act
            string realmKey = PulseRealm.RealmKeyFor(previewSceneId);

            // Assert
            Assert.That(realmKey.Length, Is.EqualTo(PulseRealm.MAX_REALM_LENGTH));
            Assert.That(realmKey, Is.EqualTo("lsd:" + previewSceneId));
        }

        [Test]
        public void HashTheKeyOneCharacterPastTheLengthLimit()
        {
            // Arrange
            var previewSceneId = new string('x', PulseRealm.MAX_REALM_LENGTH - 3);

            // Act
            string realmKey = PulseRealm.RealmKeyFor(previewSceneId);

            // Assert — lowercase hex, and the fixed 75-character overflow form that always fits
            Assert.That(realmKey, Does.Match("^lsd:sha256:[0-9a-f]{64}$"));
            Assert.That(realmKey.Length, Is.EqualTo(75));
        }

        [Test]
        public async Task ResolveTheRealmFromTheDevServerEntityId()
        {
            // Arrange
            PulseRealm realm = LocalSceneDevelopmentRealm();
            Returns(Result<LocalSceneEntity>.SuccessResult(new LocalSceneEntity(ENTITY_ID, Vector2Int.zero)));

            // Assert — the configured realm is never used in local scene development
            Assert.That(realm.Value, Is.Empty);

            // Act
            await realm.EnsureResolvedAsync(CancellationToken.None);

            // Assert
            Assert.That(realm.Value, Is.EqualTo("lsd:" + ENTITY_ID));
        }

        [Test]
        public async Task FetchOnlyOnce()
        {
            // Arrange
            PulseRealm realm = LocalSceneDevelopmentRealm();
            Returns(Result<LocalSceneEntity>.SuccessResult(new LocalSceneEntity(ENTITY_ID, Vector2Int.zero)));

            // Act
            await realm.EnsureResolvedAsync(CancellationToken.None);
            await realm.EnsureResolvedAsync(CancellationToken.None);

            // Assert
            _ = entityIdSource.Received(1).EntityAsync(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task LeaveTheRealmEmptyWhenTheDevServerIsUnreachable()
        {
            // Arrange
            PulseRealm realm = LocalSceneDevelopmentRealm();
            Returns(Result<LocalSceneEntity>.ErrorResult("Local scene server unreachable"));

            // Act
            await realm.EnsureResolvedAsync(CancellationToken.None);

            // Assert
            Assert.That(realm.Value, Is.Empty);
        }

        [Test]
        public async Task LeaveTheRealmEmptyWhenTheFetchThrows()
        {
            // Arrange
            PulseRealm realm = LocalSceneDevelopmentRealm();
            entityIdSource.EntityAsync(Arg.Any<CancellationToken>())
                          .Returns(_ => throw new InvalidOperationException("boom"));

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: boom"));

            // Act
            await realm.EnsureResolvedAsync(CancellationToken.None);

            // Assert — reported, but the log-in flow this runs inside must not fail because of it
            Assert.That(realm.Value, Is.Empty);
        }

        private PulseRealm LocalSceneDevelopmentRealm()
        {
            // A realm is still configured in local scene development; the derived key has to win over it
            realmData.RealmName.Returns("localhost");
            return new PulseRealm(realmData, entityIdSource);
        }

        private void Returns(Result<LocalSceneEntity> result) =>
            entityIdSource.EntityAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(result));

        /// <summary>
        ///     Mirrors the hashing function in js-sdk-toolchain's <c>logic/project-files.ts</c>, which mints
        ///     the entity id this client reads off the dev server.
        /// </summary>
        private static string PreviewSceneId(string projectRoot) =>
            "b64-" + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{projectRoot}-{MACHINE_ID}"));
    }
}
