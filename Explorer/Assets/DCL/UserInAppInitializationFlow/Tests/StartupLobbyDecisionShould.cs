using DCL.RealmNavigation;
using Global.AppArgs;
using NUnit.Framework;
using UnityEngine;
using static DCL.UserInAppInitializationFlow.IUserInAppInitializationFlow;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class StartupLobbyDecisionShould
    {
        [TestCase("--realm", "https://peer.decentraland.org")]
        [TestCase("--position", "10,20")]
        public void SkipTheLobbyWhenTheLaunchNamesADestination(string flag, string value)
        {
            // Arrange
            IAppArgs appArgs = new ApplicationParametersParser(false, flag, value);

            // Act
            bool show = RealUserInAppInitializationFlow.ShouldShowStartupLobby(true, appArgs, new StartParcel(Vector2Int.zero), LoadSource.StartUp);

            // Assert
            Assert.That(show, Is.False);
        }

        [Test]
        public void SkipTheLobbyWhenADeepLinkNamesADestination()
        {
            // Arrange
            IAppArgs appArgs = new ApplicationParametersParser(false, "decentraland://?realm=pravus.dcl.eth");

            // Act
            bool show = RealUserInAppInitializationFlow.ShouldShowStartupLobby(true, appArgs, new StartParcel(Vector2Int.zero), LoadSource.StartUp);

            // Assert
            Assert.That(show, Is.False);
        }

        [Test]
        public void ShowTheLobbyOnReloginEvenWhenTheLaunchNamedADestination()
        {
            // Arrange
            IAppArgs appArgs = new ApplicationParametersParser(false, "--position", "10,20");

            // Act
            bool show = RealUserInAppInitializationFlow.ShouldShowStartupLobby(true, appArgs, new StartParcel(Vector2Int.zero), LoadSource.Logout);

            // Assert
            Assert.That(show, Is.True);
        }

        [Test]
        public void ShowTheLobbyWhenTheLaunchNamesNoDestination()
        {
            // Arrange
            IAppArgs appArgs = new ApplicationParametersParser(false, "decentraland://?force-open-backpack=true");

            // Act
            bool show = RealUserInAppInitializationFlow.ShouldShowStartupLobby(true, appArgs, new StartParcel(Vector2Int.zero), LoadSource.StartUp);

            // Assert
            Assert.That(show, Is.True);
        }

        [TestCase(LoadSource.StartUp)]
        [TestCase(LoadSource.Logout)]
        public void SkipTheLobbyOnceAJumpInWasRequested(LoadSource loadSource)
        {
            // Arrange
            var startParcel = new StartParcel(Vector2Int.zero);
            startParcel.RequestJumpIn();

            // Act
            bool show = RealUserInAppInitializationFlow.ShouldShowStartupLobby(true, new ApplicationParametersParser(false), startParcel, loadSource);

            // Assert
            Assert.That(show, Is.False);
        }

        [Test]
        public void NeverShowTheLobbyWhenTheFeatureIsOff()
        {
            // Act
            bool show = RealUserInAppInitializationFlow.ShouldShowStartupLobby(false, new ApplicationParametersParser(false), new StartParcel(Vector2Int.zero), LoadSource.StartUp);

            // Assert
            Assert.That(show, Is.False);
        }
    }
}
