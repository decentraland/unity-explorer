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
using PointerType = DCL.ECSComponents.PointerType;
using ProtoVector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.PrimaryPointerInfo.Tests
{
    [TestFixture]
    public class PrimaryPointerInfoSystemShould : InputTestFixture
    {
        private const float TOLERANCE = 1e-4f;

                private World sceneWorld = null!;
                private World globalWorld = null!;
                private Mouse mouse = null!;
                private GameObject cameraGameObject = null!;
                private Camera camera = null!;
                private IECSToCRDTWriter ecsToCRDTWriter = null!;
                private ISceneStateProvider sceneStateProvider = null!;
                private IExposedCameraData exposedCameraData = null!;
                private PrimaryPointerInfoSystem system = null!;
        private CumulativePointerDelta accumulatedDelta;
                private Action<PBPrimaryPointerInfo, (Vector2 pos, Vector2 delta, ProtoVector3 rayDir)> capturedPrepare = null!;
                private List<(Vector2 pos, Vector2 delta, ProtoVector3 rayDir)> putCalls = null!;

        [SetUp]
        public void SetUp()
        {
            base.Setup();

            mouse = InputSystem.AddDevice<Mouse>();
            DCLInput.Instance.Enable();

            sceneWorld = World.Create();
            globalWorld = World.Create();

            cameraGameObject = new GameObject("PrimaryPointerInfoTestCamera");
            camera = cameraGameObject.AddComponent<Camera>();
            globalWorld.Create(new CameraComponent(camera));

            capturedPrepare = null!;
            putCalls = new List<(Vector2 pos, Vector2 delta, ProtoVector3 rayDir)>();

            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            ecsToCRDTWriter.PutMessage(
                Arg.Do<Action<PBPrimaryPointerInfo, (Vector2 pos, Vector2 delta, ProtoVector3 rayDir)>>(prepare => capturedPrepare = prepare),
                Arg.Any<CRDTEntity>(),
                Arg.Do<(Vector2 pos, Vector2 delta, ProtoVector3 rayDir)>(data => putCalls.Add(data)));

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            exposedCameraData = Substitute.For<IExposedCameraData>();
            SetPointerLocked(false);

            accumulatedDelta = default;
            exposedCameraData.AccumulatedPointerDelta.Returns(accumulatedDelta);

            system = new PrimaryPointerInfoSystem(sceneWorld, globalWorld, sceneStateProvider, ecsToCRDTWriter, exposedCameraData);
            system.Initialize();

            // Initialize() performs one PUT; discard it so tests only see Update()-driven writes.
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
        public void ReportPositionDiffDeltaWhenUnlocked()
        {
            // Arrange
            Set(mouse.position, new Vector2(100f, 100f));
            system.Update(0);

            // Act
            Set(mouse.position, new Vector2(130f, 120f));
            system.Update(0);

            // Assert
            (Vector2 pos, Vector2 delta, ProtoVector3 _) = LastPut();
            AssertVector2(new Vector2(130f, 120f), pos);
            AssertVector2(new Vector2(30f, 20f), delta);
        }

        [Test]
        public void ReportAccumulatedDeltaAndCenterCoordinatesWhenLocked()
        {
            // Arrange
            SetPointerLocked(true);
            AdvanceAccumulatedDelta(new Vector2(5f, -3f));

            // Act
            system.Update(0);

            // Assert
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            (Vector2 pos, Vector2 delta, ProtoVector3 rayDir) = LastPut();

            AssertVector2(new Vector2(5f, -3f), delta);
            AssertVector2(center, pos);

            Ray expectedRay = camera.ScreenPointToRay(center);
            Assert.AreEqual(expectedRay.direction.x, rayDir.X, TOLERANCE);
            Assert.AreEqual(expectedRay.direction.y, rayDir.Y, TOLERANCE);
            Assert.AreEqual(expectedRay.direction.z, rayDir.Z, TOLERANCE);
        }

        [Test]
        public void SumIntermediateFrameDeltasWhileLocked()
        {
            // Arrange: several render frames accumulate between two scene ticks
            SetPointerLocked(true);
            system.Update(0);

            AdvanceAccumulatedDelta(new Vector2(5f, 1f));
            AdvanceAccumulatedDelta(new Vector2(3f, 2f));
            AdvanceAccumulatedDelta(new Vector2(2f, 1f));

            // Act
            system.Update(0);

            // Assert: no intermediate frame motion is lost
            (Vector2 _, Vector2 delta, ProtoVector3 _) = LastPut();
            AssertVector2(new Vector2(10f, 4f), delta);
        }

        [Test]
        public void ReportCenterCoordinatesWhenLockedRegardlessOfPointerPosition()
        {
            // Arrange
            SetPointerLocked(true);
            Set(mouse.position, new Vector2(999f, 777f));

            // Act
            system.Update(0);

            // Assert
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            (Vector2 pos, Vector2 _, ProtoVector3 _) = LastPut();
            AssertVector2(center, pos);
        }

        [Test]
        public void NotSpikeDeltaOnUnlockTransition()
        {
            // Arrange: a locked frame at a raw position far from center
            var rawPosition = new Vector2(200f, 150f);
            Set(mouse.position, rawPosition);
            SetPointerLocked(true);
            system.Update(0);

            // Act: unlock and update at the same raw position
            SetPointerLocked(false);
            system.Update(0);

            // Assert: the raw position was tracked while locked, so the diff is zero
            (Vector2 pos, Vector2 delta, ProtoVector3 _) = LastPut();
            AssertVector2(Vector2.zero, delta);
            AssertVector2(rawPosition, pos);
        }

        [Test]
        public void NotSpikeDeltaOnLockTransition()
        {
            // Arrange: substantial accumulation while unlocked, snapshotted by intermediate updates
            AdvanceAccumulatedDelta(new Vector2(500f, 400f));
            system.Update(0);
            AdvanceAccumulatedDelta(new Vector2(300f, 200f));
            system.Update(0);

            // Act: lock with no further accumulation
            SetPointerLocked(true);
            system.Update(0);

            // Assert: the accumulated total was tracked while unlocked, so the diff is zero
            (Vector2 _, Vector2 delta, ProtoVector3 _) = LastPut();
            AssertVector2(Vector2.zero, delta);
        }

        [Test]
        public void NotWriteWhenSceneIsNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(0, putCalls.Count);
        }

        [Test]
        public void SkipPutWhenPointerStateUnchanged()
        {
            // Arrange: nothing moves — mouse stays at (0,0), unlocked, no accumulated-delta change.
            const int TICKS = 600;

            for (var i = 0; i < TICKS; i++)
                system.Update(0);

            // identical state every tick -> the gate suppresses every send.
            Assert.AreEqual(0, putCalls.Count);

            // A genuine change must reopen the gate (proves the skip is state-driven, not a dead path).
            Set(mouse.position, new Vector2(42f, 24f));
            system.Update(0);
            Assert.AreEqual(1, putCalls.Count);
        }

        // A locked pointer moving at constant velocity yields a constant NON-zero ScreenDelta every
        // tick (pos pinned to screen-center, ray direction unchanged). ScreenDelta is a per-tick
        // quantity scenes integrate, so each tick's delta must be delivered even though the
        // (pos, delta, rayDir) triple repeats verbatim — one PUT per tick of sustained motion.
        [Test]
        public void KeepSendingConstantNonZeroDeltaWhileLocked()
        {
            SetPointerLocked(true);

            const int TICKS = 10;
            var perTick = new Vector2(4f, -2f);

            for (var i = 0; i < TICKS; i++)
            {
                AdvanceAccumulatedDelta(perTick);
                system.Update(0);
            }

            Assert.AreEqual(TICKS, putCalls.Count,
                "Constant non-zero ScreenDelta must be sent every tick; integrating scenes depend on each delta.");

            (Vector2 _, Vector2 delta, ProtoVector3 _) = LastPut();
            AssertVector2(perTick, delta);
        }

        [Test]
        public void MapDataToComponentFields()
        {
            // Arrange: the PutMessage delegate captured during Initialize()
            Assert.IsNotNull(capturedPrepare);

            var component = new PBPrimaryPointerInfo();
            (Vector2 pos, Vector2 delta, ProtoVector3 rayDir) data = (new Vector2(10f, 20f), new Vector2(1f, 2f), new ProtoVector3 { X = 0.1f, Y = 0.2f, Z = 0.3f });

            // Act
            capturedPrepare(component, data);

            // Assert
            Assert.AreEqual(PointerType.PotMouse, component.PointerType);
            Assert.AreEqual(10f, component.ScreenCoordinates.X);
            Assert.AreEqual(20f, component.ScreenCoordinates.Y);
            Assert.AreEqual(1f, component.ScreenDelta.X);
            Assert.AreEqual(2f, component.ScreenDelta.Y);
            Assert.AreEqual(0.1f, component.WorldRayDirection.X);
            Assert.AreEqual(0.2f, component.WorldRayDirection.Y);
            Assert.AreEqual(0.3f, component.WorldRayDirection.Z);
        }

        private void SetPointerLocked(bool locked) =>
            exposedCameraData.PointerIsLocked.Returns(new CanBeDirty<bool>(locked));

        private void AdvanceAccumulatedDelta(Vector2 frameDelta)
        {
            accumulatedDelta += frameDelta;
            exposedCameraData.AccumulatedPointerDelta.Returns(accumulatedDelta);
        }

        private (Vector2 pos, Vector2 delta, ProtoVector3 rayDir) LastPut()
        {
            Assert.IsNotEmpty(putCalls);
            return putCalls[putCalls.Count - 1];
        }

        private static void AssertVector2(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, TOLERANCE);
            Assert.AreEqual(expected.y, actual.y, TOLERANCE);
        }
    }
}
