using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading.Tasks;
using Entity = Arch.Core.Entity;
using Quaternion = Decentraland.Common.Quaternion;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.Tween.Tests
{
    [TestFixture]
    public class TweenSequenceSystemShould : UnitySystemTestBase<TweenSequenceUpdaterSystem>
    {
        private TweenerPool tweenerPool;
        private TweenLoaderSystem loaderSystem;

        [SetUp]
        public void SetUp()
        {
            var sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            tweenerPool = new TweenerPool();
            system = new TweenSequenceUpdaterSystem(world, Substitute.For<IECSToCRDTWriter>(), tweenerPool, sceneStateProvider);
            loaderSystem = new TweenLoaderSystem(world);
        }

        [TearDown]
        public void TearDown()
        {
            system?.Dispose();
            loaderSystem?.Dispose();
        }

        [Test]
        public void LoadTweenSequenceComponent()
        {
            Vector3 startValue1 = CreateVector3(0, 0, 0);
            Vector3 endValue1 = CreateVector3(5, 0, 0);
            Vector3 startValue2 = CreateVector3(5, 0, 0);
            Vector3 endValue2 = CreateVector3(10, 0, 0);

            Entity testEntity = CreateTweenSequence(new[]
            {
                CreateMoveTween(startValue1, endValue1, 500),
                CreateMoveTween(startValue2, endValue2, 500)
            }, TweenLoop.TlRestart);

            Assert.IsTrue(world.Has<PBTweenSequence>(testEntity));
            Assert.IsFalse(world.Has<SDKTweenSequenceComponent>(testEntity));

            loaderSystem.Update(0);

            Assert.IsTrue(world.Has<SDKTweenSequenceComponent>(testEntity));
            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.IsTrue(comp.IsDirty);
        }

        [Test]
        public async Task TweenSequenceCompletesAllTweens()
        {
            Vector3 startValue1 = CreateVector3(0, 0, 0);
            Vector3 endValue1 = CreateVector3(5, 0, 0);
            Vector3 startValue2 = CreateVector3(5, 0, 0);
            Vector3 endValue2 = CreateVector3(10, 0, 0);

            Entity testEntity = CreateTweenSequence(new[]
            {
                CreateMoveTween(startValue1, endValue1, 500),
                CreateMoveTween(startValue2, endValue2, 500)
            }, TweenLoop.TlRestart);

            loaderSystem.Update(0);
            system.Update(0);

            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.IsNotNull(comp.SequenceTweener);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            // Total duration is 1000ms (500ms + 500ms)
            await RunSystemForSeconds(1000, testEntity);

            comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus);
            Assert.IsTrue(comp.SequenceTweener.IsFinished());
        }

        [Test]
        public async Task TweenSequenceWithRotateAndMove()
        {
            Vector3 startMove = CreateVector3(0, 0, 0);
            Vector3 endMove = CreateVector3(5, 0, 0);
            Quaternion startRot = CreateQuaternion(UnityEngine.Quaternion.identity);
            Quaternion endRot = CreateQuaternion(UnityEngine.Quaternion.Euler(0, 90, 0));

            Entity testEntity = CreateTweenSequence(new[]
            {
                CreateMoveTween(startMove, endMove, 500),
                CreateRotateTween(startRot, endRot, 500)
            }, TweenLoop.TlRestart);

            loaderSystem.Update(0);
            system.Update(0);

            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            await RunSystemForSeconds(1000, testEntity);

            comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus);
        }

        [Test]
        public async Task TweenSequenceWithScaleTweens()
        {
            Vector3 startScale1 = CreateVector3(1, 1, 1);
            Vector3 endScale1 = CreateVector3(2, 2, 2);
            Vector3 startScale2 = CreateVector3(2, 2, 2);
            Vector3 endScale2 = CreateVector3(1, 1, 1);

            Entity testEntity = CreateTweenSequence(new[]
            {
                CreateScaleTween(startScale1, endScale1, 500),
                CreateScaleTween(startScale2, endScale2, 500)
            }, TweenLoop.TlRestart);

            loaderSystem.Update(0);
            system.Update(0);

            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            await RunSystemForSeconds(1000, testEntity);

            comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus);
        }

        [Test]
        public void TweenSequenceLoadsWithYoYoLoop()
        {
            Vector3 startValue = CreateVector3(0, 0, 0);
            Vector3 endValue = CreateVector3(5, 0, 0);

            Entity testEntity = CreateTweenSequence(new[]
            {
                CreateMoveTween(startValue, endValue, 500)
            }, TweenLoop.TlYoyo);

            loaderSystem.Update(0);
            system.Update(0);

            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.IsNotNull(comp.SequenceTweener);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);
        }

        [Test]
        public async Task TweenSequenceWithoutLoopCompletesOnce()
        {
            Vector3 startValue1 = CreateVector3(0, 0, 0);
            Vector3 endValue1 = CreateVector3(5, 0, 0);
            Vector3 startValue2 = CreateVector3(5, 0, 0);
            Vector3 endValue2 = CreateVector3(10, 0, 0);

            Entity testEntity = CreateTweenSequenceNoLoop(new[]
            {
                CreateMoveTween(startValue1, endValue1, 500),
                CreateMoveTween(startValue2, endValue2, 500)
            });

            loaderSystem.Update(0);
            system.Update(0);

            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.IsNotNull(comp.SequenceTweener);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            // Total duration is 1000ms (500ms + 500ms)
            await RunSystemForSeconds(1000, testEntity);

            comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus);
            Assert.IsTrue(comp.SequenceTweener.IsFinished());

            // Wait more and verify it doesn't restart
            await RunSystemForSeconds(500, testEntity);

            comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus, "Sequence should remain completed without loop");
        }

        [Test]
        public async Task TweenSequenceWithMultipleTweens()
        {
            Vector3 pos1 = CreateVector3(0, 0, 0);
            Vector3 pos2 = CreateVector3(3, 0, 0);
            Vector3 pos3 = CreateVector3(3, 3, 0);
            Vector3 pos4 = CreateVector3(0, 3, 0);

            Entity testEntity = CreateTweenSequence(new[]
            {
                CreateMoveTween(pos1, pos2, 250),
                CreateMoveTween(pos2, pos3, 250),
                CreateMoveTween(pos3, pos4, 250),
                CreateMoveTween(pos4, pos1, 250)
            }, TweenLoop.TlRestart);

            loaderSystem.Update(0);
            system.Update(0);

            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            await RunSystemForSeconds(1000, testEntity);

            comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus);
        }

        private Entity CreateTweenSequence(PBTween[] tweens, TweenLoop loopType)
        {
            var crdtEntity = new CRDTEntity(1);

            // First tween becomes the PBTween component
            PBTween firstTween = tweens[0];

            var pbTweenSequence = new PBTweenSequence
            {
                Loop = loopType,
                IsDirty = true,
            };

            // Remaining tweens go into the sequence
            for (int i = 1; i < tweens.Length; i++)
            {
                pbTweenSequence.Sequence.Add(tweens[i]);
            }

            var entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            // Both PBTween and PBTweenSequence must be present
            world.Add(entity, crdtEntity, firstTween, pbTweenSequence);

            return entity;
        }

        private Entity CreateTweenSequenceNoLoop(PBTween[] tweens)
        {
            var crdtEntity = new CRDTEntity(1);

            // First tween becomes the PBTween component
            PBTween firstTween = tweens[0];

            var pbTweenSequence = new PBTweenSequence
            {
                IsDirty = true,
                // No Loop set - should play once
            };

            // Remaining tweens go into the sequence
            for (int i = 1; i < tweens.Length; i++)
            {
                pbTweenSequence.Sequence.Add(tweens[i]);
            }

            var entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            // Both PBTween and PBTweenSequence must be present
            world.Add(entity, crdtEntity, firstTween, pbTweenSequence);

            return entity;
        }

        private PBTween CreateMoveTween(Vector3 start, Vector3 end, int duration)
        {
            return new PBTween
            {
                CurrentTime = 0,
                Duration = duration,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Playing = true,
                Move = new Move { Start = start, End = end }
            };
        }

        private PBTween CreateRotateTween(Quaternion start, Quaternion end, int duration)
        {
            return new PBTween
            {
                CurrentTime = 0,
                Duration = duration,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Playing = true,
                Rotate = new Rotate { Start = start, End = end }
            };
        }

        private PBTween CreateScaleTween(Vector3 start, Vector3 end, int duration)
        {
            return new PBTween
            {
                CurrentTime = 0,
                Duration = duration,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Playing = true,
                Scale = new Scale { Start = start, End = end }
            };
        }

        private Vector3 CreateVector3(float x, float y, float z) =>
            new()
            {
                X = x,
                Y = y,
                Z = z,
            };

        private Quaternion CreateQuaternion(UnityEngine.Quaternion unityQuaternion) =>
            new()
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
                
                if (world.Has<PBTweenSequence>(testEntity))
                {
                    var pbTweenSequence = world.TryGetRef<PBTweenSequence>(testEntity, out bool exists);
                    pbTweenSequence.IsDirty = false; // simulate dirty reset system
                }
            }
        }
    }
}

