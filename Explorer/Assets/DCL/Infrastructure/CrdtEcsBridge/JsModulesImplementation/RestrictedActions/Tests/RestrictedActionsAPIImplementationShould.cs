using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Clipboard;
using DCL.CrdtEcsBridge.JsModulesImplementation;
using DCL.ECSComponents;
using DCL.ExternalUrlPrompt;
using DCL.NftPrompt;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.TeleportPrompt;
using DCL.UI;
using Decentraland.Kernel.Apis;
using ECS.TestSuite;
using MVC;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using SceneRuntime.ScenePermissions;
using System.Threading;
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
        private IExplorerUiActions explorerUiActions;
        private World sceneWorld;
        private int clipboardNotificationsReceived;

        [SetUp]
        public void SetUp()
        {
            EcsTestsUtils.SetUpFeaturesRegistry();

            NotificationsBusController.Initialize(new NotificationsBusController());
            clipboardNotificationsReceived = 0;
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.INTERNAL_SCENE_CLIPBOARD_WRITE, _ => clipboardNotificationsReceived++);

            mvcManager = Substitute.For<IMVCManager>();
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            // Stamp a recent user gesture so the OpenExplorerUi gesture gate passes by default.
            sceneStateProvider.TickNumber.Returns((uint)10);
            sceneStateProvider.LastUserInputTick.Returns((uint)10);

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
            explorerUiActions = Substitute.For<IExplorerUiActions>();
            explorerUiActions.OpenSection(Arg.Any<ExplorerUi>(), Arg.Any<ExploreSections>()).Returns(OpenExplorerUiResult.Opened);
            sceneWorld = World.Create();
            Entity scenePlayerEntity = sceneWorld.Create();
            restrictedActionsAPIImplementation = new RestrictedActionsAPIImplementation(
                mvcManager,
                sceneStateProvider,
                globalWorldActions,
                sceneData,
                new AllowEverythingJsApiPermissionsProvider(),
                systemClipboard,
                sceneWorld,
                scenePlayerEntity,
                explorerUiActions);
        }

        [TearDown]
        public void TearDown()
        {
            World.Destroy(sceneWorld);
            EcsTestsUtils.TearDownFeaturesRegistry();
            NotificationsBusController.Reset();
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
            restrictedActionsAPIImplementation.TryMovePlayerToAsync(testNewRelativePosition, testCameraTarget, testAvatarTarget, 0f, CancellationToken.None).Forget();

            // Assert
            globalWorldActions.Received(1).MoveAndRotatePlayerAsync(
                sceneData.Geometry.BaseParcelPosition + testNewRelativePosition,
                withCameraTarget ? sceneData.Geometry.BaseParcelPosition + testCameraTarget : null,
                testAvatarTarget,
                0f,
                Arg.Any<CancellationToken>());

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
        public void OpenExplorerUi_MapOpensNavmap()
        {
            // Act
            int result = restrictedActionsAPIImplementation.TryOpenExplorerUi((int)ExplorerUi.EuMap);

            // Assert
            Assert.AreEqual((int)OpenExplorerUiResult.Opened, result);
            explorerUiActions.Received(1).OpenSection(ExplorerUi.EuMap, ExploreSections.Navmap);
        }

        [Test]
        public void OpenExplorerUi_NotCurrentScene_Rejects()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);

            // Act
            int result = restrictedActionsAPIImplementation.TryOpenExplorerUi((int)ExplorerUi.EuMap);

            // Assert
            Assert.AreEqual((int)OpenExplorerUiResult.RejectedNotCurrentScene, result);
            explorerUiActions.DidNotReceive().OpenSection(Arg.Any<ExplorerUi>(), Arg.Any<ExploreSections>());
        }

        [Test]
        [TestCase(0)] // underflow-safe: no user gesture has ever been recorded
        [TestCase(5)] // stale: the last gesture is older than the allowed window
        public void OpenExplorerUi_NoRecentGesture_Rejects(int lastUserInputTick)
        {
            // Arrange
            sceneStateProvider.TickNumber.Returns((uint)10);
            sceneStateProvider.LastUserInputTick.Returns((uint)lastUserInputTick);

            // Act
            int result = restrictedActionsAPIImplementation.TryOpenExplorerUi((int)ExplorerUi.EuMap);

            // Assert
            Assert.AreEqual((int)OpenExplorerUiResult.RejectedNoUserGesture, result);
            explorerUiActions.DidNotReceive().OpenSection(Arg.Any<ExplorerUi>(), Arg.Any<ExploreSections>());
        }

        [Test]
        public void OpenExplorerUi_AlreadyOpen_ReturnsWasAlreadyOpen()
        {
            // Arrange
            explorerUiActions.OpenSection(Arg.Any<ExplorerUi>(), Arg.Any<ExploreSections>()).Returns(OpenExplorerUiResult.WasAlreadyOpen);

            // Act
            int result = restrictedActionsAPIImplementation.TryOpenExplorerUi((int)ExplorerUi.EuMap);

            // Assert
            Assert.AreEqual((int)OpenExplorerUiResult.WasAlreadyOpen, result);
        }

        [Test]
        public void OpenExplorerUi_UnknownUiValue_Rejects()
        {
            // Act: 99 is not a member of the ExplorerUi enum, so the section mapping must fail.
            int result = restrictedActionsAPIImplementation.TryOpenExplorerUi(99);

            // Assert
            Assert.AreEqual((int)OpenExplorerUiResult.RejectedFeatureDisabled, result);
            explorerUiActions.DidNotReceive().OpenSection(Arg.Any<ExplorerUi>(), Arg.Any<ExploreSections>());
        }

        [Test]
        public void OpenExplorerUi_FeatureDisabled_Rejects()
        {
            // Arrange
            // CAMERA_REEL is force-enabled in the editor, so an app-args override is the only way
            // to exercise the disabled branch of the features-registry gate.
            EcsTestsUtils.TearDownFeaturesRegistry();
            EcsTestsUtils.SetUpFeaturesRegistryWithAppArgs(new[] { "--camera-reel", "false" });

            // Act
            int result = restrictedActionsAPIImplementation.TryOpenExplorerUi((int)ExplorerUi.EuCameraReel);

            // Assert
            Assert.AreEqual((int)OpenExplorerUiResult.RejectedFeatureDisabled, result);
            explorerUiActions.DidNotReceive().OpenSection(Arg.Any<ExplorerUi>(), Arg.Any<ExploreSections>());
        }

        [Test]
        public void OpenExplorerUi_CommunitiesRejectionPropagates()
        {
            // Arrange
            // Communities availability is identity-dependent, so its gate lives inside the
            // IExplorerUiActions implementation; the API must return that rejection to the scene.
            explorerUiActions.OpenSection(ExplorerUi.EuCommunities, ExploreSections.Communities).Returns(OpenExplorerUiResult.RejectedFeatureDisabled);

            // Act
            int result = restrictedActionsAPIImplementation.TryOpenExplorerUi((int)ExplorerUi.EuCommunities);

            // Assert
            Assert.AreEqual((int)OpenExplorerUiResult.RejectedFeatureDisabled, result);
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
        public void CopyToClipboard_NotifiesTheUser()
        {
            // Act
            restrictedActionsAPIImplementation.TryCopyToClipboard("Ia Ia! Cthulhu Ftaghn!");

            // Assert
            Assert.AreEqual(1, clipboardNotificationsReceived);
        }

        [Test]
        public void CopyToClipboard_NotifiesOnEveryWrite()
        {
            // Act: a scene calling from onUpdate writes on every tick
            for (var i = 0; i < 10; i++)
                restrictedActionsAPIImplementation.TryCopyToClipboard($"0xATTACKER{i}");

            // Assert: the API reports every write; collapsing repeats into a single toast is the
            // notification controller's job, so that it can uncollapse as soon as the toast is gone.
            systemClipboard.Received(10).Set(Arg.Any<string>());
            Assert.AreEqual(10, clipboardNotificationsReceived);
        }

        [Test]
        public void CopyToClipboard_DoesNotNotify_WhenSceneIsNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);

            // Act
            restrictedActionsAPIImplementation.TryCopyToClipboard("This should not be copied");

            // Assert
            Assert.AreEqual(0, clipboardNotificationsReceived);
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
            restrictedActionsAPIImplementation.TryMovePlayerToAsync(relativePosition, null, null, 0f, CancellationToken.None).Forget();

            // Assert
            // Should not call MoveAndRotatePlayerAsync because position is invalid
            globalWorldActions.DidNotReceive().MoveAndRotatePlayerAsync(Arg.Any<Vector3>(), Arg.Any<Vector3?>(), Arg.Any<Vector3?>(), Arg.Any<float>(), Arg.Any<CancellationToken>());
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
            restrictedActionsAPIImplementation.TryMovePlayerToAsync(relativePosition, null, null, 0f, CancellationToken.None).Forget();

            // Assert
            // Portable Experiences should allow positions outside their scene boundaries
            globalWorldActions.Received(1).MoveAndRotatePlayerAsync(
                positionOutsideScene,
                null,
                null,
                0f,
                Arg.Any<CancellationToken>());
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
            restrictedActionsAPIImplementation.TryMovePlayerToAsync(relativePosition, null, null, 0f, CancellationToken.None).Forget();

            // Assert
            // Regular scenes should allow positions within their scene boundaries
            globalWorldActions.Received(1).MoveAndRotatePlayerAsync(
                positionInsideScene,
                null,
                null,
                0f,
                Arg.Any<CancellationToken>());
        }

        [Test]
        public void MovePlayerTo_PassesDurationParameter()
        {
            // Arrange
            Vector3 testPosition = new Vector3(5, 0, 5);
            float testDuration = 2.5f;

            // Act
            restrictedActionsAPIImplementation.TryMovePlayerToAsync(testPosition, null, null, testDuration, CancellationToken.None).Forget();

            // Assert
            globalWorldActions.Received(1).MoveAndRotatePlayerAsync(
                Arg.Any<Vector3>(),
                Arg.Any<Vector3?>(),
                Arg.Any<Vector3?>(),
                testDuration,
                Arg.Any<CancellationToken>());
        }

        [Test]
        public void MovePlayerTo_WithDuration_CallsGlobalWorldActions()
        {
            // Arrange
            Vector3 testPosition = new Vector3(5, 0, 5);
            Vector3 cameraTarget = new Vector3(10, 5, 10);
            Vector3 avatarTarget = new Vector3(15, 0, 10);
            float testDuration = 3f;

            // Act
            restrictedActionsAPIImplementation.TryMovePlayerToAsync(testPosition, cameraTarget, avatarTarget, testDuration, CancellationToken.None).Forget();

            // Assert
            globalWorldActions.Received(1).MoveAndRotatePlayerAsync(
                sceneData.Geometry.BaseParcelPosition + testPosition,
                sceneData.Geometry.BaseParcelPosition + cameraTarget,
                avatarTarget,
                testDuration,
                Arg.Any<CancellationToken>());

            globalWorldActions.Received(1).RotateCamera(
                sceneData.Geometry.BaseParcelPosition + cameraTarget,
                sceneData.Geometry.BaseParcelPosition + testPosition);
        }

        [Test]
        public void MovePlayerTo_WithZeroDuration_CallsGlobalWorldActionsWithZeroDuration()
        {
            // Arrange
            Vector3 testPosition = new Vector3(5, 0, 5);

            // Act
            restrictedActionsAPIImplementation.TryMovePlayerToAsync(testPosition, null, null, 0f, CancellationToken.None).Forget();

            // Assert
            globalWorldActions.Received(1).MoveAndRotatePlayerAsync(
                Arg.Any<Vector3>(),
                Arg.Any<Vector3?>(),
                Arg.Any<Vector3?>(),
                0f,
                Arg.Any<CancellationToken>());
        }

        [Test]
        public void TryTriggerEmote_WithMask_RoutesThroughSceneWorld()
        {
            // Arrange
            const string EMOTE_URN = "urn:emote:foo";

            // Act
            restrictedActionsAPIImplementation.TryTriggerEmote(EMOTE_URN, AvatarEmoteMask.AemUpperBody);

            // Assert: masked path taken (TriggerEmote on global world is NOT called for masked emotes —
            // they go through the scene world via TriggerMaskedEmoteOnSceneWorld instead)
            globalWorldActions.DidNotReceive().TriggerEmote(Arg.Any<URN>(), Arg.Any<bool>(), Arg.Any<AvatarEmoteMask>());
        }

        [Test]
        public void TryTriggerSceneEmoteAsync_WithMask_PreservesMask()
        {
            // Arrange
            const string SRC = "scene/foo_emote.glb";
            const string HASH = "QmFakeHash";
            StubSceneContentHash(SRC, HASH);

            // Act
            restrictedActionsAPIImplementation.TryTriggerSceneEmoteAsync(SRC, false, AvatarEmoteMask.AemUpperBody, CancellationToken.None).Forget();

            // Assert: original mask is preserved
            globalWorldActions.Received(1).TriggerSceneEmoteAsync(sceneData, SRC, HASH, false, AvatarEmoteMask.AemUpperBody, Arg.Any<CancellationToken>());
        }

        private void StubSceneContentHash(string src, string hash)
        {
            var sceneContent = Substitute.For<ISceneContent>();
            sceneContent.TryGetHash(src, out Arg.Any<string>())
                        .Returns(call =>
                         {
                             call[1] = hash;
                             return true;
                         });
            sceneData.SceneContent.Returns(sceneContent);
        }
    }
}
