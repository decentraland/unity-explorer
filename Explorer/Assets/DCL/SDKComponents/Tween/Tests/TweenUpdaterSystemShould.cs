using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Systems;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Arch.Core;
using ECS.Unity.Materials.Components;
using SceneRunner.Scene;
using Entity = Arch.Core.Entity;
using Quaternion = Decentraland.Common.Quaternion;
using Vector2 = Decentraland.Common.Vector2;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.Tween.Tests
{
    [TestFixture]
    public class TweenUpdaterSystemShould : UnitySystemTestBase<TweenUpdaterSystem>
    {
        [SetUp]
        public void SetUp()
        {
            tweneerPool = new TweenerPool();
            system = new TweenUpdaterSystem(world, Substitute.For<IECSToCRDTWriter>(), tweneerPool, Substitute.For<SceneStateProvider>(), null, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            system?.Dispose();
        }

        private Entity entity;
        private PBTween pbTween;
        private TweenerPool tweneerPool;

        private const float DEFAULT_CURRENT_TIME_0 = 0f;
        private const float DEFAULT_CURRENT_TIME_1 = 1f;
        private const float DURATION = 1000;

        private void CreateTransformTween<TMode>(
            Vector3? startVec3 = null,
            Vector3? endVec3 = null,
            Quaternion? startRotation = null,
            Quaternion? endRotation = null,
            Vector2? startVec2 = null,
            Vector2? endVec2 = null,
            TextureMovementType movementType = TextureMovementType.TmtOffset) where TMode: class, new()
        {
            var crdtEntity = new CRDTEntity(1);
            var tweenMode = new TMode();

            Move? move = null;
            Rotate? rotate = null;
            Scale? scale = null;
            TextureMove? textureMove = null;

            switch (tweenMode)
            {
                case Move m:
                    move = m;
                    move.Start = startVec3;
                    move.End = endVec3;
                    break;
                case Rotate r:
                    rotate = r;
                    rotate.Start = startRotation;
                    rotate.End = endRotation;
                    break;
                case Scale s:
                    scale = s;
                    scale.Start = startVec3;
                    scale.End = endVec3;
                    break;
                case TextureMove tm:
                    textureMove = tm;
                    textureMove.Start = startVec2;
                    textureMove.End = endVec2;
                    textureMove.MovementType = movementType;
                    break;
            }

            pbTween = new PBTween
            {
                CurrentTime = DEFAULT_CURRENT_TIME_0,
                Duration = DURATION,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Move = move,
                Rotate = rotate,
                Scale = scale,
                TextureMove = textureMove,
                Playing = true,
            };

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);

            world.Add(entity, crdtEntity, pbTween);
            system.Update(0);
        }

        private Vector2 CreateVector2(int x, int y) =>
            new ()
            {
                X = x,
                Y = y,
            };

        private Vector3 CreateVector3(int x, int y, int z) =>
            new ()
            {
                X = x,
                Y = y,
                Z = z,
            };

        private Quaternion CreateQuaternion(UnityEngine.Quaternion unityQuaternion) =>
            new ()
            {
                X = unityQuaternion.x,
                Y = unityQuaternion.y,
                Z = unityQuaternion.z,
                W = unityQuaternion.w,
            };

        [Test]
        public void ChangingPBTweenCurrentTimeUpdatesTheTweenStateStatus()
        {
            CreateTransformTween<Move>(CreateVector3(0, 0, 0), CreateVector3(1, 1, 1));

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsActive));

            pbTween.CurrentTime = DEFAULT_CURRENT_TIME_1;
            pbTween.IsDirty = true;
            system.Update(0);

            //We need a second update, to move the state from playing to complete.
            system.Update(0);

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsCompleted));
        }

        [Test]
        public void ChangingPBTweenPlayingValueUpdatesTheTweenStateStatus()
        {
            CreateTransformTween<Move>(CreateVector3(0, 0, 0), CreateVector3(1, 1, 1));

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsActive));

            pbTween.Playing = false;
            pbTween.IsDirty = true;

            system.Update(0);

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsPaused));

            pbTween.Playing = true;
            pbTween.IsDirty = true;

            system.Update(0);

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
                Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsActive));
        }

        [Test]
        public void TweenTextureMoveOffsetUpdatesToFinalValueAfterDuration()
        {
            Vector2 startValue = CreateVector2(0, 0);
            Vector2 endValue = CreateVector2(8, 2);
            CreateTransformTween<TextureMove>(startVec2: startValue, endVec2: endValue, movementType: TextureMovementType.TmtOffset);

            SimulateTime();

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
            {
                var tweener = (QuaternionTweener)comp.CustomTweener;
                Assert.AreEqual(endValue.X, tweener.CurrentValue.x, 0.01f, "X value should reach the end value.");
                Assert.AreEqual(endValue.Y, tweener.CurrentValue.y, 0.01f, "Y value should reach the end value.");
                Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");
            });

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref MaterialComponent materialComponent) =>
            {
                UnityEngine.Vector2 matOffset = materialComponent.Result!.mainTextureOffset;
                Assert.AreEqual(endValue.X, matOffset.x, 0.01f, "Material offset X value should reach the end value.");
                Assert.AreEqual(endValue.Y, matOffset.y, 0.01f, "Material offset Y value should reach the end value.");
            });
        }

        [Test]
        public void TweenTextureMoveTilingUpdatesToFinalValueAfterDuration()
        {
            Vector2 startValue = CreateVector2(0, 0);
            Vector2 endValue = CreateVector2(8, 2);
            CreateTransformTween<TextureMove>(startVec2: startValue, endVec2: endValue, movementType: TextureMovementType.TmtTiling);

            SimulateTime();

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
            {
                var tweener = (QuaternionTweener)comp.CustomTweener;
                Assert.AreEqual(endValue.X, tweener.CurrentValue.x, 0.01f, "X value should reach the end value.");
                Assert.AreEqual(endValue.Y, tweener.CurrentValue.y, 0.01f, "Y value should reach the end value.");
                Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");
            });

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref MaterialComponent materialComponent) =>
            {
                UnityEngine.Vector2 matTiling = materialComponent.Result!.mainTextureScale;
                Assert.AreEqual(endValue.X, matTiling.x, 0.01f, "Material tiling X value should reach the end value.");
                Assert.AreEqual(endValue.Y, matTiling.y, 0.01f, "Material tiling Y value should reach the end value.");
            });
        }

        [Test]
        public void TweenRotateUpdatesToFinalValueAfterDuration()
        {
            Quaternion startValue = CreateQuaternion(UnityEngine.Quaternion.identity);
            Quaternion endValue = CreateQuaternion(UnityEngine.Quaternion.Euler(new UnityEngine.Vector3(180, 0, 0)));
            CreateTransformTween<Move>(startRotation: startValue, endRotation: endValue);

            SimulateTime();

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
            {
                var tweener = (QuaternionTweener)comp.CustomTweener;
                Assert.AreEqual(endValue.X, tweener.CurrentValue.x, 0.01f, "X value should reach the end value.");
                Assert.AreEqual(endValue.Y, tweener.CurrentValue.y, 0.01f, "Y value should reach the end value.");
                Assert.AreEqual(endValue.Z, tweener.CurrentValue.z, 0.01f, "Z value should reach the end value.");
                Assert.AreEqual(endValue.W, tweener.CurrentValue.w, 0.01f, "W value should reach the end value.");
                Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");
            });
        }

        [Test]
        public void TweenMoveUpdatesToFinalValueAfterDuration()
        {
            TweenUpdatesToFinalValueAfterDuration<Move>();
        }

        [Test]
        public void TweenScaleUpdatesToFinalValueAfterDuration()
        {
            TweenUpdatesToFinalValueAfterDuration<Scale>();
        }

        public void TweenUpdatesToFinalValueAfterDuration<T>() where T: class, new()
        {
            Vector3 endValue = CreateVector3(10, 0, 5);
            CreateTransformTween<T>(CreateVector3(0, 0, 0), endValue);

            SimulateTime();

            world.Query(new QueryDescription().WithAll<SDKTweenComponent>(), (ref SDKTweenComponent comp) =>
            {
                var tweener = (Vector3Tweener)comp.CustomTweener;
                Assert.AreEqual(endValue.X, tweener.CurrentValue.x, 0.01f, "X value should reach the end value.");
                Assert.AreEqual(endValue.Y, tweener.CurrentValue.y, 0.01f, "Y value should reach the end value.");
                Assert.AreEqual(endValue.Z, tweener.CurrentValue.z, 0.01f, "Z value should reach the end value.");
                Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");
            });
        }

        private void SimulateTime()
        {
            const float updateInterval = 0.1f;
            float currentInterval = 0;

            while (currentInterval < DURATION)
            {
                currentInterval += updateInterval;
                system.Update(currentInterval);
                pbTween.CurrentTime += updateInterval;
            }
        }
    }
}
