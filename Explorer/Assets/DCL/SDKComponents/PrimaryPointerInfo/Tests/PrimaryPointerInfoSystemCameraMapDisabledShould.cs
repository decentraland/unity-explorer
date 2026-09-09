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
    // action map (ChatInputBlockingService.Block() -> Kind.Camera), and PrimaryPointerInfoSystem
    // sources its raw pointer position from `DCLInput.Instance.Camera.Point`, which is part of
    // that map. A disabled InputAction.ReadValue<Vector2>() returns default(Vector2), so the
    // system used to feed PBPrimaryPointerInfo.ScreenCoordinates = (0,0) to every scene for as
    // long as chat stayed focused, pinning scene-side UI (e.g. the Genesis Plaza fishing pond's
    // "Toggle Hints" tooltip) to the bottom-left corner instead of the real cursor.
    [TestFixture]
    public class PrimaryPointerInfoSystemCameraMapDisabledShould : InputTestFixture
    {
        private const float TOLERANCE = 1e-4f;

        private World sceneWorld;
        private World globalWorld;
        private Mouse mouse;
        private GameObject cameraGameObject;
        private Camera camera;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private ISceneStateProvider sceneStateProvider;
        private IExposedCameraData exposedCameraData;
        private PrimaryPointerInfoSystem system;
        private List<(Vector2 pos, Vector2 delta, ProtoVector3 rayDir)> putCalls;

        [SetUp]
        public void SetUp()
        {
            base.Setup();

            mouse = InputSystem.AddDevice<Mouse>();
            DCLInput.Instance.Enable();

            sceneWorld = World.Create();
            globalWorld = World.Create();

            cameraGameObject = new GameObject("PrimaryPointerInfoCameraMapDisabledTestCamera");
            camera = cameraGameObject.AddComponent<Camera>();
            globalWorld.Create(new CameraComponent(camera));

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
            // Arrange: position the simulated pointer device, then disable the whole `Camera`
            // action map exactly as ApplyInputMapsSystem.cs does when chat gains focus
            // (DCLInput.Instance.Camera.Disable()) - this puts every action in that map,
            // including Point, into the Disabled phase.
            var simulatedPosition = new Vector2(456f, 234f);
            Set(mouse.position, simulatedPosition);
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
            Assert.AreEqual(simulatedPosition.x, pos.x, TOLERANCE);
            Assert.AreEqual(simulatedPosition.y, pos.y, TOLERANCE);
        }
    }
}
