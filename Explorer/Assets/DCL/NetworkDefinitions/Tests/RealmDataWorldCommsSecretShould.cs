using CommunicationData.URLHelpers;
using DCL.Ipfs;
using NSubstitute;
using NUnit.Framework;

namespace ECS.Tests
{
    public class RealmDataWorldCommsSecretShould
    {
        private static readonly URLDomain WORLD_A = URLDomain.FromString("https://worlds.example.com/world/private-world.dcl.eth");
        private static readonly URLDomain REALM_B = URLDomain.FromString("https://attacker.example.com");
        private const string PASSWORD = "example-password-123";

        private RealmData realmData = null!;
        private IIpfsRealm ipfsRealm = null!;

        [SetUp]
        public void SetUp()
        {
            realmData = new RealmData();
            ipfsRealm = Substitute.For<IIpfsRealm>();
        }

        [Test]
        public void StartWithEmptySecret()
        {
            Assert.IsEmpty(realmData.WorldCommsSecret);
        }

        [Test]
        public void ApplyPendingSecretWhenConfiguredRealmMatchesValidatedScope()
        {
            //Arrange
            realmData.SetPendingWorldCommsSecret(WORLD_A, PASSWORD);

            //Act
            Reconfigure(WORLD_A);

            //Assert
            Assert.AreEqual(PASSWORD, realmData.WorldCommsSecret);
        }

        [Test]
        public void ClearSecretWhenReconfiguredToDifferentRealm()
        {
            //Arrange
            realmData.SetPendingWorldCommsSecret(WORLD_A, PASSWORD);
            Reconfigure(WORLD_A);

            //Act
            Reconfigure(REALM_B);

            //Assert
            Assert.IsEmpty(realmData.WorldCommsSecret);
        }

        [Test]
        public void ClearSecretOnInvalidate()
        {
            //Arrange
            realmData.SetPendingWorldCommsSecret(WORLD_A, PASSWORD);
            Reconfigure(WORLD_A);

            //Act
            realmData.Invalidate();

            //Assert
            Assert.IsEmpty(realmData.WorldCommsSecret);
        }

        [Test]
        public void RestoreSecretAfterInvalidateWhenValidatedRealmIsConfigured()
        {
            //Arrange: mirrors the real transition order — password validated, then Invalidate, then Reconfigure.
            realmData.SetPendingWorldCommsSecret(WORLD_A, PASSWORD);
            realmData.Invalidate();

            //Act
            Reconfigure(WORLD_A);

            //Assert
            Assert.AreEqual(PASSWORD, realmData.WorldCommsSecret);
        }

        [Test]
        public void KeepSecretWhenSameRealmIsReconfigured()
        {
            //Arrange: realm-change retries and /reload reconfigure the same realm without re-validation.
            realmData.SetPendingWorldCommsSecret(WORLD_A, PASSWORD);
            Reconfigure(WORLD_A);

            //Act
            realmData.Invalidate();
            Reconfigure(WORLD_A);

            //Assert
            Assert.AreEqual(PASSWORD, realmData.WorldCommsSecret);
        }

        [Test]
        public void NotRestoreSecretWhenReturningToWorldWithoutRevalidation()
        {
            //Arrange
            realmData.SetPendingWorldCommsSecret(WORLD_A, PASSWORD);
            Reconfigure(WORLD_A);

            //Act
            Reconfigure(REALM_B);
            Reconfigure(WORLD_A);

            //Assert
            Assert.IsEmpty(realmData.WorldCommsSecret);
        }

        [Test]
        public void NeverApplySecretWhenReconfiguredWithoutRealmUrl()
        {
            //Arrange
            realmData.SetPendingWorldCommsSecret(WORLD_A, PASSWORD);

            //Act
            Reconfigure(default);

            //Assert
            Assert.IsEmpty(realmData.WorldCommsSecret);
        }

        [Test]
        public void IgnorePendingSecretScopedToDefaultRealm()
        {
            //Arrange
            realmData.SetPendingWorldCommsSecret(default, PASSWORD);

            //Act
            Reconfigure(default);

            //Assert
            Assert.IsEmpty(realmData.WorldCommsSecret);
        }

        [Test]
        public void DropPendingSecretOnClear()
        {
            //Arrange
            realmData.SetPendingWorldCommsSecret(WORLD_A, PASSWORD);
            realmData.ClearPendingWorldCommsSecret();

            //Act
            Reconfigure(WORLD_A);

            //Assert
            Assert.IsEmpty(realmData.WorldCommsSecret);
        }

        private void Reconfigure(URLDomain realmUrl) =>
            realmData.Reconfigure(ipfsRealm, "realm", 1, "wss://comms.example.com", "v3", "hostname.example.com", false, WorldManifest.Empty, realmUrl: realmUrl);
    }
}
