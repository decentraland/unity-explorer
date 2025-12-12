using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Systems;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials;
using System.Threading.Tasks;
using Entity = Arch.Core.Entity;
using Quaternion = Decentraland.Common.Quaternion;
using Vector2 = Decentraland.Common.Vector2;
using Vector3 = Decentraland.Common.Vector3;

namespace DCL.SDKComponents.Tween.Tests
{
    [TestFixture]
    public class TweenSequenceSystemShould : UnitySystemTestBase<TweenSequenceUpdaterSystem>
    {
        private TweenerPool tweenerPool;
        private TweenSequenceLoaderSystem loaderSystem;
        private ISceneStateProvider sceneStateProvider;
        private IECSToCRDTWriter ecsToCRDTWriter;

        [SetUp]
        public void SetUp()
        {
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            tweenerPool = new TweenerPool();
            system = new TweenSequenceUpdaterSystem(world, ecsToCRDTWriter, tweenerPool, sceneStateProvider);
            loaderSystem = new TweenSequenceLoaderSystem(world);
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
        }

        [Test]
        public async Task TweenSequenceWithRotateAndMove()
        {
            Vector3 startMove = CreateVector3(0, 0, 0);
            Vector3 endMove = CreateVector3(5, 0, 0);
            Quaternion startRot = CreateQuaternion(UnityEngine.Quaternion.identity);
            Quaternion endRot = CreateQuaternion(UnityEngine.Quaternion.Euler(0, 90, 0));

            Entity testEntity = CreateTweenSequenceNoLoop(new[]
            {
                CreateMoveTween(startMove, endMove, 500),
                CreateRotateTween(startRot, endRot, 500)
            });

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

            Entity testEntity = CreateTweenSequenceNoLoop(new[]
            {
                CreateScaleTween(startScale1, endScale1, 500),
                CreateScaleTween(startScale2, endScale2, 500)
            });

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
        public async Task TweenSequenceUpdatesSDKTransform()
        {
            Vector3 startValue = CreateVector3(0, 0, 0);
            Vector3 endValue = CreateVector3(10, 0, 0);

            Entity testEntity = CreateTweenSequenceNoLoop(new[]
            {
                CreateMoveTween(startValue, endValue, 500)
            });

            loaderSystem.Update(0);
            system.Update(0);

            // Initial SDKTransform should be at start
            var sdkTransform = world.Get<CrdtEcsBridge.Components.Transform.SDKTransform>(testEntity);
            Assert.AreEqual(startValue.X, sdkTransform.Position.Value.x, 0.01f);

            // Wait for sequence to complete
            await RunSystemForSeconds(600, testEntity);

            // SDKTransform should be updated to end value (verifying sync works)
            sdkTransform = world.Get<CrdtEcsBridge.Components.Transform.SDKTransform>(testEntity);
            Assert.AreEqual(endValue.X, sdkTransform.Position.Value.x, 0.5f, "SDKTransform should be synced to Unity Transform at end");
        }

        [Test]
        public async Task TweenSequenceWritesTransformToCRDT()
        {
            Vector3 startValue = CreateVector3(0, 0, 0);
            Vector3 endValue = CreateVector3(10, 0, 0);

            Entity testEntity = CreateTweenSequenceNoLoop(new[]
            {
                CreateMoveTween(startValue, endValue, 500)
            });

            loaderSystem.Update(0);
            system.Update(0);

            // Clear any calls from setup
            ecsToCRDTWriter.ClearReceivedCalls();

            // Should write transform updates to CRDT during sequence
            await RunSystemForSeconds(250, testEntity);

            // Verify CRDT writer was called with SDKTransform updates
            ecsToCRDTWriter.Received().PutMessage<CrdtEcsBridge.Components.Transform.SDKTransform, CrdtEcsBridge.Components.Transform.SDKTransform>(
                Arg.Any<System.Action<CrdtEcsBridge.Components.Transform.SDKTransform, CrdtEcsBridge.Components.Transform.SDKTransform>>(),
                Arg.Any<CRDTEntity>(),
                Arg.Any<CrdtEcsBridge.Components.Transform.SDKTransform>());
        }

        [Test]
        public async Task TweenSequenceMarksSDKTransformDirtyWhenSceneNotCurrent()
        {
            Vector3 startValue = CreateVector3(0, 0, 0);
            Vector3 endValue = CreateVector3(10, 0, 0);

            // Start with scene current to set up the sequence
            sceneStateProvider.IsCurrent.Returns(true);

            Entity testEntity = CreateTweenSequenceNoLoop(new[]
            {
                CreateMoveTween(startValue, endValue, 500)
            });

            loaderSystem.Update(0);
            system.Update(0);

            // Now change scene state to not current
            sceneStateProvider.IsCurrent.Returns(false);

            await RunSystemForSeconds(250, testEntity);

            // SDKTransform should be marked as dirty (not updating cache) when scene is not current
            var sdkTransform = world.Get<CrdtEcsBridge.Components.Transform.SDKTransform>(testEntity);
            Assert.IsTrue(sdkTransform.IsDirty, "SDKTransform should be marked dirty when scene is not current");

            // Verify SDKTransform was synced from Unity Transform (even though scene not current)
            Assert.Greater(sdkTransform.Position.Value.x, startValue.X, "SDKTransform should still be synced from Unity Transform");
        }

        [Test]
        public async Task TweenSequenceWithMultipleTweens()
        {
            Vector3 pos1 = CreateVector3(0, 0, 0);
            Vector3 pos2 = CreateVector3(3, 0, 0);
            Vector3 pos3 = CreateVector3(3, 3, 0);
            Vector3 pos4 = CreateVector3(0, 3, 0);

            Entity testEntity = CreateTweenSequenceNoLoop(new[]
            {
                CreateMoveTween(pos1, pos2, 250),
                CreateMoveTween(pos2, pos3, 250),
                CreateMoveTween(pos3, pos4, 250),
                CreateMoveTween(pos4, pos1, 250)
            });

            loaderSystem.Update(0);
            system.Update(0);

            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);

            await RunSystemForSeconds(1000, testEntity);

            comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus);
        }

        [Test]
        public async Task TextureMoveSequenceUpdatesMaterial()
        {
            // Create a material
            var material = new UnityEngine.Material(UnityEngine.Shader.Find("DCL/Universal Render Pipeline/Lit"));
            var materialComponent = new MaterialComponent(MaterialData.CreateBasicMaterial(null, null, 0, UnityEngine.Color.white, false))
            {
                Result = material
            };

            Vector2 start = new Vector2 { X = 0, Y = 0 };
            Vector2 mid = new Vector2 { X = 0.5f, Y = 0 };
            Vector2 end = new Vector2 { X = 1, Y = 0 };

            Entity testEntity = CreateTweenSequenceNoLoop(new[]
            {
                CreateTextureMoveTween(start, mid, 500, TextureMovementType.TmtOffset),
                CreateTextureMoveTween(mid, end, 500, TextureMovementType.TmtOffset)
            });

            world.Add(testEntity, materialComponent);

            loaderSystem.Update(0);
            system.Update(0);

            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);
            Assert.IsNotNull(comp.SequenceTweener);

            // Run for a short time to verify animation has started but not finished first tween
            await RunSystemForSeconds(100, testEntity);

            // Check material offset (should be > 0 and < 0.5)
            // We use the property ID that the tweener uses
            int propertyId = UnityEngine.Shader.PropertyToID("_BaseMap");
            UnityEngine.Vector2 offset = material.GetTextureOffset(propertyId);

            Assert.Greater(offset.x, 0f);
            Assert.Less(offset.x, 0.5f);

            // Run for enough time to ensure completion of both tweens (500 + 500 = 1000ms total)
            // We add extra buffer to be safe
            await RunSystemForSeconds(1500, testEntity);

            offset = material.GetTextureOffset(propertyId);
            Assert.AreEqual(1.0f, offset.x, 0.01f);

            comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus);

            // Clean up
            UnityEngine.Object.DestroyImmediate(material);
        }

        [Test]
        public async Task TextureMoveTweenSequenceDoesntWriteTransformToCRDT()
        {
            // Create a material
            var material = new UnityEngine.Material(UnityEngine.Shader.Find("DCL/Universal Render Pipeline/Lit"));
            var materialComponent = new MaterialComponent(MaterialData.CreateBasicMaterial(null, null, 0, UnityEngine.Color.white, false))
            {
                Result = material
            };

            Vector2 start = new Vector2 { X = 0, Y = 0 };
            Vector2 end = new Vector2 { X = 1, Y = 0 };

            Entity testEntity = CreateTweenSequenceNoLoop(new[]
            {
                CreateTextureMoveTween(start, end, 500, TextureMovementType.TmtOffset)
            });

            world.Add(testEntity, materialComponent);

            loaderSystem.Update(0);
            system.Update(0);

            // Clear any calls from setup
            ecsToCRDTWriter.ClearReceivedCalls();

            SDKTweenSequenceComponent comp = world.Get<SDKTweenSequenceComponent>(testEntity);
            Assert.AreEqual(TweenStateStatus.TsActive, comp.TweenStateStatus);
            Assert.IsFalse(comp.HasTransformTweens);

            // Run for 250ms (halfway)
            await RunSystemForSeconds(250, testEntity);

            // Verify CRDT writer was NOT called with SDKTransform updates
            ecsToCRDTWriter.DidNotReceive().PutMessage(
                Arg.Any<System.Action<CrdtEcsBridge.Components.Transform.SDKTransform, CrdtEcsBridge.Components.Transform.SDKTransform>>(),
                Arg.Any<CRDTEntity>(),
                Arg.Any<CrdtEcsBridge.Components.Transform.SDKTransform>());

            // It SHOULD write TweenState
            ecsToCRDTWriter.Received().PutMessage(
                Arg.Any<System.Action<PBTweenState, TweenStateStatus>>(),
                Arg.Any<CRDTEntity>(),
                Arg.Any<TweenStateStatus>());

            // Clean up
            UnityEngine.Object.DestroyImmediate(material);
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

        private PBTween CreateTextureMoveTween(Vector2 start, Vector2 end, int duration, TextureMovementType movementType)
        {
            return new PBTween
            {
                CurrentTime = 0,
                Duration = duration,
                EasingFunction = EasingFunction.EfLinear,
                IsDirty = true,
                Playing = true,
                TextureMove = new TextureMove { Start = start, End = end, MovementType = movementType }
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

                    var pbTween = world.TryGetRef<PBTween>(testEntity, out bool exists2);
                    pbTween.IsDirty = false; // simulate dirty reset system
                }
            }
        }
    }
}

