using DCL.ChangeRealmPrompt;
using DCL.ExternalUrlPrompt;
using DCL.NftPrompt;
using DCL.TeleportPrompt;
using MVC;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    public class RestrictedActionsAPIImplementationShould
    {
        private RestrictedActionsAPIImplementation restrictedActionsAPIImplementation;
        private IMVCManager mvcManager;
        private ISceneStateProvider sceneStateProvider;
        private IGlobalWorldActions globalWorldActions;
        private ISceneData sceneData;

        [SetUp]
        public void SetUp()
        {
            mvcManager = Substitute.For<IMVCManager>();
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            globalWorldActions = Substitute.For<IGlobalWorldActions>();
            sceneData = Substitute.For<ISceneData>();
            sceneData.Geometry.Returns(ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY);
            sceneData.Parcels.Returns(new []
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
            });
            restrictedActionsAPIImplementation = new RestrictedActionsAPIImplementation(
                mvcManager,
                sceneStateProvider,
                globalWorldActions,
                sceneData);
        }

        [Test]
        public void OpenExternalUrl()
        {
            // Arrange
            var testUrl = "www.test.com";

            // Act
            restrictedActionsAPIImplementation.TryOpenExternalUrl(testUrl);

            // Assert
            mvcManager.Received(1).ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(testUrl)));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MovePlayerTo(bool withCameraTarget)
        {
            // Arrange
            Vector3 testNewRelativePosition = new Vector3(5, 5, 3);
            Vector3? testCameraTarget = withCameraTarget ? new Vector3(5, 3, 2) : null;

            // Act
            restrictedActionsAPIImplementation.TryMovePlayerTo(testNewRelativePosition, testCameraTarget);

            // Assert
            globalWorldActions.Received(1).MoveAndRotatePlayer(
                sceneData.Geometry.BaseParcelPosition + testNewRelativePosition,
                sceneData.Geometry.BaseParcelPosition + testCameraTarget);

            globalWorldActions.Received(1).RotateCamera(
                withCameraTarget ? sceneData.Geometry.BaseParcelPosition + testCameraTarget : null,
                sceneData.Geometry.BaseParcelPosition + testNewRelativePosition);
        }

        [Test]
        public void TeleportTo()
        {
            // Arrange
            Vector2Int testCoords = new Vector2Int(10, 20);

            // Act
            restrictedActionsAPIImplementation.TryTeleportTo(testCoords);

            // Assert
            mvcManager.Received(1).ShowAsync(TeleportPromptController.IssueCommand(new TeleportPromptController.Params(testCoords)));
        }

        [Test]
        public void ChangeRealm()
        {
            // Arrange
            const string TEST_MESSAGE = "TestMessage";
            const string TEST_REALM = "TestRealm";

            // Act
            restrictedActionsAPIImplementation.TryChangeRealm(TEST_MESSAGE, TEST_REALM);

            // Assert
            mvcManager.Received(1).ShowAsync(ChangeRealmPromptController.IssueCommand(new ChangeRealmPromptController.Params(TEST_MESSAGE, TEST_REALM)));
        }

        [Test]
        public void OpenNftDialog()
        {
            // Arrange
            const string TEST_URN = "urn:decentraland:ethereum:erc721:0x06012c8cf97bead5deae237070f9587f8e7a266d:1540722";

            // Act
            bool result = restrictedActionsAPIImplementation.TryOpenNftDialog(TEST_URN);

            // Assert
            mvcManager.Received(1).ShowAsync(NftPromptController.IssueCommand(new NftPromptController.Params("0x06012c8cf97bead5deae237070f9587f8e7a266d", "1540722")));
        }
    }
}
