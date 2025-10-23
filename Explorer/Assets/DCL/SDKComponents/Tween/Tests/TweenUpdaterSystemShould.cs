using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using ECS.Unity.Materials.Components;
using SceneRunner.Scene;
using System.Threading.Tasks;
using Utility.Primitives;
using Entity = Arch.Core.Entity;
using Quaternion = Decentraland.Common.Quaternion;
using Vector2 = Decentraland.Common.Vector2;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.Tween.Tests
{
    [TestFixture]
    public class TweenUpdaterSystemShould : UnitySystemTestBase<TweenUpdaterSystem>
    {
        private TweenerPool tweneerPool;

        [SetUp]
        public void SetUp()
        {
            var sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            tweneerPool = new TweenerPool();
            system = new TweenUpdaterSystem(world, Substitute.For<IECSToCRDTWriter>(), tweneerPool, sceneStateProvider);
        }

        [TearDown]
        public void TearDown()
        {
            system?.Dispose();
        }

        [Test]
        public async Task ChangingPBTweenCurrentTimeUpdatesTheTweenStateStatus()
        {
            Vector3 startValue = CreateVector3(0, 0, 0);
            Vector3 endValue = CreateVector3(10, 0, 5);
            int duration = 500;
            Entity testEntity = CreateTransformTween<Move>(duration, startValue, endValue);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsActive);

            var pbTween = world.TryGetRef<PBTween>(testEntity, out bool exists);
            pbTween.CurrentTime = 1f;
            pbTween.IsDirty = true;
            system!.Update(0);

            await RunSystemForSeconds(10, testEntity);

            system.Update(0);

            comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsCompleted);
        }

        [Test]
        public void ChangingPBTweenPlayingValueUpdatesTheTweenStateStatus()
        {
            Vector3 startValue = CreateVector3(0, 0, 0);
            Vector3 endValue = CreateVector3(10, 0, 5);
            int duration = 500;
            Entity testEntity = CreateTransformTween<Move>(duration, startValue, endValue);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsActive);

            var pbTween = world.TryGetRef<PBTween>(testEntity, out bool exists);
            pbTween.Playing = false;
            pbTween.IsDirty = true;

            system.Update(0);

            comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsPaused);

            pbTween = world.TryGetRef<PBTween>(testEntity, out bool exists2);
            pbTween.Playing = true;
            pbTween.IsDirty = true;

            system.Update(0);

            comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.IsTrue(comp.TweenStateStatus == TweenStateStatus.TsActive);
        }

        [Test]
        public async Task TweenTextureMoveOffsetUpdatesToFinalValueAfterDuration()
        {
            Vector2 startValue = CreateVector2(1, 1);
            Vector2 endValue = CreateVector2(8, 2);
            int duration = 500;
            Entity testEntity = CreateTransformTween<TextureMove>(duration, startVec2: startValue, endVec2: endValue, movementType: TextureMovementType.TmtOffset);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            var tweener = (Vector2Tweener)comp.CustomTweener;
            Assert.AreEqual(startValue.X, tweener.CurrentValue.x, 0.01f);
            Assert.AreEqual(startValue.Y, tweener.CurrentValue.y, 0.01f);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            Assert.IsTrue(world.Has<MaterialComponent>(testEntity));
            MaterialComponent materialComponent = world.Get<MaterialComponent>(testEntity);
            UnityEngine.Vector2 matTiling = materialComponent.Result!.mainTextureOffset;
            Assert.AreEqual(startValue.X, matTiling.x, 0.01f);
            Assert.AreEqual(startValue.Y, matTiling.y, 0.01f);

            await RunSystemForSeconds(duration, testEntity);

            comp = world.Get<SDKTweenComponent>(testEntity);
            tweener = (Vector2Tweener)comp.CustomTweener;
            Assert.AreEqual(endValue.X, tweener.CurrentValue.x, 0.01f, "X value should reach the end value.");
            Assert.AreEqual(endValue.Y, tweener.CurrentValue.y, 0.01f, "Y value should reach the end value.");
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");

            materialComponent = world.Get<MaterialComponent>(testEntity);
            matTiling = materialComponent.Result!.mainTextureOffset;
            Assert.AreEqual(endValue.X, matTiling.x, 0.01f, "Material tiling X value should reach the end value.");
            Assert.AreEqual(endValue.Y, matTiling.y, 0.01f, "Material tiling Y value should reach the end value.");
        }

        [Test]
        public async Task TweenTextureMoveTilingUpdatesToFinalValueAfterDuration()
        {
            Vector2 startValue = CreateVector2(1, 1);
            Vector2 endValue = CreateVector2(8, 2);
            int duration = 500;
            Entity testEntity = CreateTransformTween<TextureMove>(duration, startVec2: startValue, endVec2: endValue, movementType: TextureMovementType.TmtTiling);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            var tweener = (Vector2Tweener)comp.CustomTweener;
            Assert.AreEqual(startValue.X, tweener.CurrentValue.x, 0.01f);
            Assert.AreEqual(startValue.Y, tweener.CurrentValue.y, 0.01f);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            Assert.IsTrue(world.Has<MaterialComponent>(testEntity));
            MaterialComponent materialComponent = world.Get<MaterialComponent>(testEntity);
            UnityEngine.Vector2 matTiling = materialComponent.Result!.mainTextureScale;
            Assert.AreEqual(startValue.X, matTiling.x, 0.01f);
            Assert.AreEqual(startValue.Y, matTiling.y, 0.01f);

            await RunSystemForSeconds(duration, testEntity);

            comp = world.Get<SDKTweenComponent>(testEntity);
            tweener = (Vector2Tweener)comp.CustomTweener;
            Assert.AreEqual(endValue.X, tweener.CurrentValue.x, 0.01f, "X value should reach the end value.");
            Assert.AreEqual(endValue.Y, tweener.CurrentValue.y, 0.01f, "Y value should reach the end value.");
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");

            materialComponent = world.Get<MaterialComponent>(testEntity);
            matTiling = materialComponent.Result!.mainTextureScale;
            Assert.AreEqual(endValue.X, matTiling.x, 0.01f, "Material tiling X value should reach the end value.");
            Assert.AreEqual(endValue.Y, matTiling.y, 0.01f, "Material tiling Y value should reach the end value.");
        }

        [Test]
        public async Task TweenRotateUpdatesToFinalValueAfterDuration()
        {
            Quaternion startValue = CreateQuaternion(UnityEngine.Quaternion.identity);
            Quaternion endValue = CreateQuaternion(UnityEngine.Quaternion.Euler(new UnityEngine.Vector3(179, 0, 0)));
            int duration = 500;
            Entity testEntity = CreateTransformTween<Rotate>(duration, startRotation: startValue, endRotation: endValue);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            var tweener = (QuaternionTweener)comp.CustomTweener;
            Assert.AreEqual(startValue.X, tweener.CurrentValue.x, 0.01f);
            Assert.AreEqual(startValue.Y, tweener.CurrentValue.y, 0.01f);
            Assert.AreEqual(startValue.Z, tweener.CurrentValue.z, 0.01f);
            Assert.AreEqual(startValue.W, tweener.CurrentValue.w, 0.01f);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            await RunSystemForSeconds(duration, testEntity);

            comp = world.Get<SDKTweenComponent>(testEntity);
            tweener = (QuaternionTweener)comp.CustomTweener;
            Assert.AreEqual(endValue.X, tweener.CurrentValue.x, 0.01f, "X value should reach the end value.");
            Assert.AreEqual(endValue.Y, tweener.CurrentValue.y, 0.01f, "Y value should reach the end value.");
            Assert.AreEqual(endValue.Z, tweener.CurrentValue.z, 0.01f, "Z value should reach the end value.");
            Assert.AreEqual(endValue.W, tweener.CurrentValue.w, 0.01f, "W value should reach the end value.");
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");
        }

        [Test]
        public async Task TweenMoveUpdatesToFinalValueAfterDuration()
        {
            await TweenUpdatesToFinalValueAfterDuration<Move>();
        }

        [Test]
        public async Task TweenScaleUpdatesToFinalValueAfterDuration()
        {
            await TweenUpdatesToFinalValueAfterDuration<Scale>();
        }

        public async Task TweenUpdatesToFinalValueAfterDuration<T>() where T: class, new()
        {
            Vector3 startValue = CreateVector3(0, 0, 0);
            Vector3 endValue = CreateVector3(10, 0, 5);
            int duration = 500;
            Entity testEntity = CreateTransformTween<T>(duration, startValue, endValue);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            var tweener = (Vector3Tweener)comp.CustomTweener;
            Assert.AreEqual(startValue.X, tweener.CurrentValue.x, 0.01f);
            Assert.AreEqual(startValue.Y, tweener.CurrentValue.y, 0.01f);
            Assert.AreEqual(startValue.Z, tweener.CurrentValue.z, 0.01f);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            await RunSystemForSeconds(duration, testEntity);

            comp = world.Get<SDKTweenComponent>(testEntity);
            tweener = (Vector3Tweener)comp.CustomTweener;
            Assert.AreEqual(endValue.X, tweener.CurrentValue.x, 0.01f, "X value should reach the end value.");
            Assert.AreEqual(endValue.Y, tweener.CurrentValue.y, 0.01f, "Y value should reach the end value.");
            Assert.AreEqual(endValue.Z, tweener.CurrentValue.z, 0.01f, "Z value should reach the end value.");
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");
        }

        [Test]
        public async Task RotateContinuousCompletesAfterDuration()
        {
            var rotDir = CreateQuaternion(UnityEngine.Quaternion.Euler(new UnityEngine.Vector3(0, 0, 1)));
            int duration = 1000; // Duration in milliseconds
            Entity testEntity = CreateContinuousTween<RotateContinuous>(duration, rotateDirection: rotDir, speed: 90f);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);
            Assert.IsNotNull(comp.CustomTweener);
            Assert.IsInstanceOf<QuaternionTweener>(comp.CustomTweener);

            await RunSystemForSeconds(duration, testEntity);

            Assert.IsTrue(world.Has<SDKTweenComponent>(testEntity));
            comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");
            Assert.IsTrue(comp.CustomTweener.IsFinished(), "Tweener should be marked as finished.");

            var finishedValue = ((QuaternionTweener)comp.CustomTweener).CurrentValue;

            // Continue updating for 500ms more - value should not change after completion
            await RunSystemForSeconds(500, testEntity);

            comp = world.Get<SDKTweenComponent>(testEntity);
            var currentValue = ((QuaternionTweener)comp.CustomTweener).CurrentValue;
            Assert.AreEqual(finishedValue.x, currentValue.x, 0.001f, "Rotation should not change after tween completes.");
            Assert.AreEqual(finishedValue.y, currentValue.y, 0.001f, "Rotation should not change after tween completes.");
            Assert.AreEqual(finishedValue.z, currentValue.z, 0.001f, "Rotation should not change after tween completes.");
            Assert.AreEqual(finishedValue.w, currentValue.w, 0.001f, "Rotation should not change after tween completes.");
        }

        [Test]
        public async Task MoveContinuousMovesAndCompletesAfterDuration()
        {
            var dir = CreateVector3(1, 0, 0);
            const float speed = 2f;
            int duration = 1000; // Duration in milliseconds
            Entity testEntity = CreateContinuousTween<MoveContinuous>(duration, moveDirection: dir, speed: speed);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);
            Assert.IsNotNull(comp.CustomTweener);
            Assert.IsInstanceOf<Vector3Tweener>(comp.CustomTweener);

            await RunSystemForSeconds(duration, testEntity);

            Assert.IsTrue(world.Has<SDKTweenComponent>(testEntity));
            comp = world.Get<SDKTweenComponent>(testEntity);
            var tweener = (Vector3Tweener)comp.CustomTweener;
            Assert.AreEqual(speed, tweener.CurrentValue.x, 0.05f, "X value should have moved by speed.");
            Assert.AreEqual(0f, tweener.CurrentValue.y, 0.01f, "Y value should remain at 0.");
            Assert.AreEqual(0f, tweener.CurrentValue.z, 0.01f, "Z value should remain at 0.");
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");
            Assert.IsTrue(comp.CustomTweener.IsFinished(), "Tweener should be marked as finished.");

            var finishedValue = tweener.CurrentValue;

            // Continue updating for 500ms more - value should not change after completion
            await RunSystemForSeconds(500, testEntity);

            comp = world.Get<SDKTweenComponent>(testEntity);
            tweener = (Vector3Tweener)comp.CustomTweener;
            Assert.AreEqual(finishedValue.x, tweener.CurrentValue.x, 0.001f, "Position should not change after tween completes.");
            Assert.AreEqual(finishedValue.y, tweener.CurrentValue.y, 0.001f, "Position should not change after tween completes.");
            Assert.AreEqual(finishedValue.z, tweener.CurrentValue.z, 0.001f, "Position should not change after tween completes.");
        }

        [Test]
        public async Task TextureMoveContinuousOffsetCompletesAndUpdatesMaterial()
        {
            var texDir = CreateVector2(1, 0);
            const float speed = 3f;
            int duration = 1000; // Duration in milliseconds
            Entity testEntity = CreateContinuousTween<TextureMoveContinuous>(duration, textureDirection: texDir, speed: speed, movementType: TextureMovementType.TmtOffset);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);
            Assert.IsNotNull(comp.CustomTweener);
            Assert.IsInstanceOf<Vector2Tweener>(comp.CustomTweener);

            Assert.IsTrue(world.Has<MaterialComponent>(testEntity));
            MaterialComponent materialComponent = world.Get<MaterialComponent>(testEntity);
            UnityEngine.Vector2 startOffset = materialComponent.Result!.mainTextureOffset;

            await RunSystemForSeconds(duration, testEntity);

            Assert.IsTrue(world.Has<SDKTweenComponent>(testEntity));
            comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Tween should be marked as completed.");
            Assert.IsTrue(comp.CustomTweener.IsFinished(), "Tweener should be marked as finished.");

            materialComponent = world.Get<MaterialComponent>(testEntity);
            UnityEngine.Vector2 matOffset = materialComponent.Result!.mainTextureOffset;
            // TODO: this is a flaky test.. results varies depending on the cpu timing
            Assert.AreEqual(speed, matOffset.x, 0.3f, "Material offset X should have moved by speed.");
            Assert.AreEqual(0f, matOffset.y, 0.3f, "Material offset Y should remain at 0.");

            var finishedOffset = matOffset;

            // Continue updating for 500ms more - value should not change after completion
            await RunSystemForSeconds(500, testEntity);

            materialComponent = world.Get<MaterialComponent>(testEntity);
            matOffset = materialComponent.Result!.mainTextureOffset;
            Assert.AreEqual(finishedOffset.x, matOffset.x, 0.001f, "Material offset should not change after tween completes.");
            Assert.AreEqual(finishedOffset.y, matOffset.y, 0.001f, "Material offset should not change after tween completes.");
        }

        [Test]
        public async Task ContinuousTweensRunIndefinitelyWhenDurationIsZero()
        {
            var dir = CreateVector3(0, 1, 0);
            const float speed = 1f;
            int duration = 0; // Zero duration means infinite
            Entity testEntity = CreateContinuousTween<MoveContinuous>(duration, moveDirection: dir, speed: speed);

            SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);
            Assert.IsNotNull(comp.CustomTweener);
            Assert.IsInstanceOf<Vector3Tweener>(comp.CustomTweener);

            var tweener = (Vector3Tweener)comp.CustomTweener;
            var startValue = tweener.CurrentValue;

            // Simulate 2000ms, should not complete and should keep moving
            await RunSystemForSeconds(2000, testEntity);

            Assert.IsTrue(world.Has<SDKTweenComponent>(testEntity));
            comp = world.Get<SDKTweenComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus, "Tween should still be active after 2 seconds.");
            Assert.IsFalse(comp.CustomTweener.IsFinished(), "Tweener should not be finished.");

            tweener = (Vector3Tweener)comp.CustomTweener;
            // Value should have changed (moved in Y direction)
            Assert.Greater(tweener.CurrentValue.y, startValue.y, "Tween should continue to move indefinitely.");
        }

        private Entity CreateTransformTween<TMode>(
            float duration,
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

            var pbTween = new PBTween
            {
                CurrentTime = 0,
                Duration = duration,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Playing = true,
            };

            switch (tweenMode)
            {
                case Move move:
                    move.Start = startVec3;
                    move.End = endVec3;
                    pbTween.Move = move;
                    Assert.AreEqual(PBTween.ModeOneofCase.Move, pbTween.ModeCase);
                    break;
                case Rotate rotate:
                    rotate.Start = startRotation;
                    rotate.End = endRotation;
                    pbTween.Rotate = rotate;
                    Assert.AreEqual(PBTween.ModeOneofCase.Rotate, pbTween.ModeCase);
                    break;
                case Scale scale:
                    scale.Start = startVec3;
                    scale.End = endVec3;
                    pbTween.Scale = scale;
                    Assert.AreEqual(PBTween.ModeOneofCase.Scale, pbTween.ModeCase);
                    break;
                case TextureMove textureMove:
                    textureMove.Start = startVec2;
                    textureMove.End = endVec2;
                    textureMove.MovementType = movementType;
                    pbTween.TextureMove = textureMove;
                    Assert.AreEqual(PBTween.ModeOneofCase.TextureMove, pbTween.ModeCase);
                    break;
            }

            var entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);

            world.Add(entity, crdtEntity, pbTween,
                new MaterialComponent { Result = DefaultMaterial.New() }, // for TextureMove
                new SDKTweenComponent // simulating TweenLoaderSystem
                {
                    IsDirty = true,
                }
            );
            system.Update(0);

            Assert.IsTrue(world.Has<SDKTweenComponent>(entity));

            return entity;
        }

        private Entity CreateContinuousTween<TMode>(
            float duration,
            Vector3? moveDirection = null,
            Quaternion? rotateDirection = null,
            Vector2? textureDirection = null,
            float speed = 1f,
            TextureMovementType movementType = TextureMovementType.TmtOffset) where TMode: class, new()
        {
            var crdtEntity = new CRDTEntity(2);
            var tweenMode = new TMode();

            var pbTween = new PBTween
            {
                CurrentTime = 0,
                Duration = duration,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Playing = true,
            };

            switch (tweenMode)
            {
                case MoveContinuous moveContinuous:
                    moveContinuous.Direction = moveDirection;
                    moveContinuous.Speed = speed;
                    pbTween.MoveContinuous = moveContinuous;
                    Assert.AreEqual(PBTween.ModeOneofCase.MoveContinuous, pbTween.ModeCase);
                    break;
                case RotateContinuous rotateContinuous:
                    rotateContinuous.Direction = rotateDirection;
                    rotateContinuous.Speed = speed;
                    pbTween.RotateContinuous = rotateContinuous;
                    Assert.AreEqual(PBTween.ModeOneofCase.RotateContinuous, pbTween.ModeCase);
                    break;
                case TextureMoveContinuous textureMoveContinuous:
                    textureMoveContinuous.Direction = textureDirection;
                    textureMoveContinuous.Speed = speed;
                    textureMoveContinuous.MovementType = movementType;
                    pbTween.TextureMoveContinuous = textureMoveContinuous;
                    Assert.AreEqual(PBTween.ModeOneofCase.TextureMoveContinuous, pbTween.ModeCase);
                    break;
            }

            var entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);

            world.Add(entity, crdtEntity, pbTween,
                new MaterialComponent { Result = DefaultMaterial.New() }, // for TextureMove
                new SDKTweenComponent // simulating TweenLoaderSystem
                {
                    IsDirty = true,
                }
            );
            system.Update(0);

            Assert.IsTrue(world.Has<SDKTweenComponent>(entity));

            return entity;
        }

        private Vector2 CreateVector2(float x, float y) =>
            new ()
            {
                X = x,
                Y = y,
            };

        private Vector3 CreateVector3(float x, float y, float z) =>
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

        private async Task RunSystemForSeconds(int durationInMs, Entity testEntity)
        {
            int updateInterval = 1;
            float currentInterval = 0;
            while (currentInterval < durationInMs)
            {
                await Task.Delay(updateInterval);
                currentInterval += updateInterval;
                system!.Update(updateInterval);
                var pbTween = world.TryGetRef<PBTween>(testEntity, out bool exists);
                pbTween.IsDirty = false; // simulate dirty reset system
            }
        }
    }
}
