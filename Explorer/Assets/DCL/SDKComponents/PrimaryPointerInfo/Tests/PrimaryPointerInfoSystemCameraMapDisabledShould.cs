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
    // Regression coverage for https://github.com/decentraland/unity-explorer/issues/9496: while chat is
    // focused, ApplyInputMapsSystem disables the whole `Camera` action map, and a disabled
    // InputAction.ReadValue<Vector2>() returns default(Vector2). The pointer feed must therefore come from
    // IExposedCameraData.PointerScreenPosition, which no action map can disable.
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
            // Arrange: disable the whole `Camera` action map, as ApplyInputMapsSystem does when chat gains focus.
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
            Assert.AreEqual(Screen.height - SIMULATED_POSITION.y, pos.y, TOLERANCE);
        }
    }
}
