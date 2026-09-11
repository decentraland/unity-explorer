using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.SDKComponents.PrimaryPointerInfo.Systems;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;
using ProtoVector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.PrimaryPointerInfo.Tests
{
    // Regression coverage for https://github.com/decentraland/unity-explorer/issues/9496:
    // while the explorer chat is focused, ApplyInputMapsSystem disables the whole `Camera`
    // action map (ChatInputBlockingService.Block() -> Kind.Camera). PrimaryPointerInfoSystem used
    // to source its pointer position from `DCLInput.Instance.Camera.Point`, an action in that map,
    // and a disabled InputAction.ReadValue<Vector2>() returns default(Vector2) - so the system fed
    // PBPrimaryPointerInfo.ScreenCoordinates = (0,0) to every scene for as long as chat stayed
    // focused, pinning scene-side UI (e.g. the Genesis Plaza fishing pond's "Toggle Hints"
    // tooltip) to the bottom-left corner instead of the real cursor.
    // The feed now takes the pointer the cursor pipeline resolved (IExposedCameraData.
    // PointerScreenPosition), which no action map can disable; this fixture keeps the guard so
    // routing the feed back through an input action fails a test instead of a scene.
    [TestFixture]
    public class PrimaryPointerInfoSystemCameraMapDisabledShould : InputTestFixture
    {
        private const float TOLERANCE = 1e-4f;

        private static readonly Vector2 SIMULATED_POSITION = new (456f, 234f);

        private World sceneWorld = null!;
        private World globalWorld = null!;
        private Mouse mouse = null!;
        private GameObject cameraGameObject = null!;
        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private ISceneStateProvider sceneStateProvider = null!;
        private IExposedCameraData exposedCameraData = null!;
        private PrimaryPointerInfoSystem system = null!;
        private List<(Vector2 pos, Vector2 delta, ProtoVector3 rayDir)> putCalls = null!;

        [SetUp]
        public void SetUp()
        {
            base.Setup();

            mouse = InputSystem.AddDevice<Mouse>();
            DCLInput.Instance.Enable();

            sceneWorld = World.Create();
            globalWorld = World.Create();

            cameraGameObject = new GameObject("PrimaryPointerInfoCameraMapDisabledTestCamera");
            globalWorld.Create(new CameraComponent(cameraGameObject.AddComponent<Camera>()));

            putCalls = new List<(Vector2 pos, Vector2 delta, ProtoVector3 rayDir)>();

            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            ecsToCRDTWriter.PutMessage(
                Arg.Any<Action<PBPrimaryPointerInfo, (Vector2 pos, Vector2 delta, ProtoVector3 rayDir)>>(),
                Arg.Any<CRDTEntity>(),
                Arg.Do<(Vector2 pos, Vector2 delta, ProtoVector3 rayDir)>(data => putCalls.Add(data)));

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            exposedCameraData = Substitute.For<IExposedCameraData>();
            exposedCameraData.PointerIsLocked.Returns(new CanBeDirty<bool>(false));
            exposedCameraData.AccumulatedPointerDelta.Returns(default(CumulativePointerDelta));
            exposedCameraData.PointerScreenPosition.Returns(SIMULATED_POSITION);

            system = new PrimaryPointerInfoSystem(sceneWorld, globalWorld, sceneStateProvider, ecsToCRDTWriter, exposedCameraData);
            system.Initialize();

            // Initialize() performs one PUT; discard it so the test only sees the Update()-driven write.
            putCalls.Clear();
        }

        [TearDown]
        public void Cleanup()
        {
            system.Dispose();
            UnityEngine.Object.DestroyImmediate(cameraGameObject);
            sceneWorld.Dispose();
            globalWorld.Dispose();
        }

        [Test]
        public void NotReportZeroScreenCoordinatesWhenCameraMapDisabledByChatFocus()
        {
            // Arrange: park the pointer device where the cursor pipeline reports it, then disable the
            // whole `Camera` action map exactly as ApplyInputMapsSystem.cs does when chat gains focus
            // (DCLInput.Instance.Camera.Disable()) - this puts every action in that map, including
            // Point, into the Disabled phase.
            Set(mouse.position, SIMULATED_POSITION);
            DCLInput.Instance.Camera.Disable();

            // Act
            system.Update(0);

            // Assert: the scene-facing pointer feed must keep tracking the real cursor instead
            // of collapsing to (0,0) - the exact value a disabled InputAction.ReadValue<Vector2>()
            // returns, and the value that pins the fishing-pond "Toggle Hints" tooltip to the
            // bottom-left corner while chat is focused.
            Assert.IsNotEmpty(putCalls);
            (Vector2 pos, Vector2 _, ProtoVector3 _) = putCalls[putCalls.Count - 1];

            Assert.AreNotEqual(Vector2.zero, pos, "screenCoordinates must not collapse to (0,0) while the Camera map is disabled");
            Assert.AreEqual(SIMULATED_POSITION.x, pos.x, TOLERANCE);
            Assert.AreEqual(SIMULATED_POSITION.y, pos.y, TOLERANCE);
        }
    }
}
