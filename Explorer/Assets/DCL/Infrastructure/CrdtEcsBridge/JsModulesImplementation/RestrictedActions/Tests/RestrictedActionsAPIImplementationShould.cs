using DCL.ChangeRealmPrompt;
using DCL.Clipboard;
using DCL.ExternalUrlPrompt;
using DCL.NftPrompt;
using DCL.TeleportPrompt;
using MVC;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using SceneRuntime.ScenePermissions;
using UnityEngine;
using UnityEngine.TestTools;
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
        private ISystemClipboard systemClipboard;

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
            systemClipboard = Substitute.For<ISystemClipboard>();
            restrictedActionsAPIImplementation = new RestrictedActionsAPIImplementation(
                mvcManager,
                sceneStateProvider,
                globalWorldActions,
                sceneData,
                new AllowEverythingJsApiPermissionsProvider(),
                systemClipboard);
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
        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(false, false)]
        public void MovePlayerTo(bool withCameraTarget, bool withRotation)
        {
            // Arrange
            Vector3 testNewRelativePosition = new Vector3(5, 5, 3);
            Vector3? testCameraTarget = withCameraTarget ? new Vector3(5, 3, 2) : null;
            Vector3? testAvatarTarget = withCameraTarget ? new Vector3(2, 6, -3) : null;

            // Act
            restrictedActionsAPIImplementation.TryMovePlayerTo(testNewRelativePosition, testCameraTarget, testAvatarTarget);

            // Assert
            globalWorldActions.Received(1).MoveAndRotatePlayer(
                sceneData.Geometry.BaseParcelPosition + testNewRelativePosition,
                withCameraTarget ? sceneData.Geometry.BaseParcelPosition + testCameraTarget : null,
                testAvatarTarget,
                0f);

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
            mvcManager.Received(1).ShowAsync(NftPromptController.IssueCommand(new NftPromptController.Params("ethereum", "0x06012c8cf97bead5deae237070f9587f8e7a266d", "1540722")));
        }

        [Test]
        public void CopyToClipboard()
        {
            // Arrange
            const string TEST_TEXT = "Ia Ia! Cthulhu Ftaghn!";

            // Act
            restrictedActionsAPIImplementation.TryCopyToClipboard(TEST_TEXT);

            // Assert
            systemClipboard.Received(1).Set(TEST_TEXT);
        }

        [Test]
        public void CopyToClipboard_DoesNotCopy_WhenSceneIsNotCurrent()
        {
            // Arrange
            const string TEST_TEXT = "This should not be copied";
            sceneStateProvider.IsCurrent.Returns(false);

            // Act
            restrictedActionsAPIImplementation.TryCopyToClipboard(TEST_TEXT);

            // Assert
            systemClipboard.DidNotReceive().Set(Arg.Any<string>());
        }

        [Test]
        public void MovePlayerTo_RejectsPositionOutsideScene_ForRegularScene()
        {
            // Arrange
            sceneData.IsPortableExperience().Returns(false);
            // Position that maps to parcel (10, 10) which is not in the scene parcels (0,0), (0,1), (0,2)
            Vector3 positionOutsideScene = new Vector3(160, 0, 160); // Parcel (10, 10)
            Vector3 relativePosition = positionOutsideScene - sceneData.Geometry.BaseParcelPosition;

            // Act
            LogAssert.Expect(LogType.Error, "MovePlayerTo: Position is out of scene");
            restrictedActionsAPIImplementation.TryMovePlayerTo(relativePosition, null, null);

            // Assert
            // Should not call MoveAndRotatePlayer because position is invalid
            globalWorldActions.DidNotReceive().MoveAndRotatePlayer(Arg.Any<Vector3>(), Arg.Any<Vector3?>(), Arg.Any<Vector3?>(), Arg.Any<float>());
        }

        [Test]
        public void MovePlayerTo_AllowsPositionOutsideScene_ForPortableExperience()
        {
            // Arrange
            sceneData.IsPortableExperience().Returns(true);
            // Position that maps to parcel (10, 10) which is not in the scene parcels (0,0), (0,1), (0,2)
            Vector3 positionOutsideScene = new Vector3(160, 0, 160); // Parcel (10, 10)
            Vector3 relativePosition = positionOutsideScene - sceneData.Geometry.BaseParcelPosition;

            // Act
            restrictedActionsAPIImplementation.TryMovePlayerTo(relativePosition, null, null);

            // Assert
            // Portable Experiences should allow positions outside their scene boundaries
            globalWorldActions.Received(1).MoveAndRotatePlayer(
                positionOutsideScene,
                null,
                null,
                0f);
        }

        [Test]
        public void MovePlayerTo_AllowsPositionInsideScene_ForRegularScene()
        {
            // Arrange
            sceneData.IsPortableExperience().Returns(false);
            // Position that maps to parcel (0, 1) which IS in the scene parcels
            Vector3 positionInsideScene = new Vector3(0, 0, 16); // Parcel (0, 1)
            Vector3 relativePosition = positionInsideScene - sceneData.Geometry.BaseParcelPosition;

            // Act
            restrictedActionsAPIImplementation.TryMovePlayerTo(relativePosition, null, null);

            // Assert
            // Regular scenes should allow positions within their scene boundaries
            globalWorldActions.Received(1).MoveAndRotatePlayer(
                positionInsideScene,
                null,
                null,
                0f);
        }
    }
}
