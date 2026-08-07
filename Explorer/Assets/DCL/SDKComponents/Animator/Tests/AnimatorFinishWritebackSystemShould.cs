using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.Animator.Components;
using DCL.SDKComponents.Animator.Systems;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Asset.Tests;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.Animator.Tests
{
    [TestFixture]
    public class AnimatorFinishWritebackSystemShould : UnitySystemTestBase<AnimatorFinishWritebackSystem>
    {
        private Entity entity;
        private PBAnimator pbAnimator;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private SDKAnimatorUpdaterSystem sdkAnimatorUpdaterSystem;
        private LegacyAnimationPlayerSystem legacyAnimationPlayerSystem;
        private CreateGltfAssetFromAssetBundleSystem createGltfAssetFromAssetBundleSystem;
        private FinalizeGltfContainerLoadingSystem finalizeGltfContainerLoadingSystem;
        private GltfContainerTestResources resources;

        [SetUp]
        public void SetUp()
        {
            // A fresh instance per test: the fixture instance is shared across tests, and tests that load
            // no bundle must not re-dispose the previous test's already-unloaded bundle in teardown.
            resources = new GltfContainerTestResources();

            Entity sceneRoot = world.Create(new SceneRootComponent());
            AddTransformToEntity(sceneRoot);
            IReleasablePerformanceBudget releasablePerformanceBudget = Substitute.For<IReleasablePerformanceBudget>();
            releasablePerformanceBudget.TrySpendBudget().Returns(true);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.Geometry.Returns(ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY);

            finalizeGltfContainerLoadingSystem = new FinalizeGltfContainerLoadingSystem(world, sceneRoot, releasablePerformanceBudget,
                NullEntityCollidersSceneCache.INSTANCE, sceneData, new EntityEventBuffer<GltfContainerComponent>(1));
            IReleasablePerformanceBudget budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
            createGltfAssetFromAssetBundleSystem = new CreateGltfAssetFromAssetBundleSystem(world, budget, budget);

            sdkAnimatorUpdaterSystem = new SDKAnimatorUpdaterSystem(world);
            legacyAnimationPlayerSystem = new LegacyAnimationPlayerSystem(world);

            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            system = new AnimatorFinishWritebackSystem(world, ecsToCRDTWriter);

            pbAnimator = new PBAnimator
            {
                States =
                {
                    new PBAnimationState
                    {
                        Clip = "bite",
                        Loop = false,
                        Playing = true,
                        Speed = 1,
                        Weight = 1,
                        ShouldReset = false,
                    },
                    new PBAnimationState
                    {
                        Clip = "swim",
                        Loop = true,
                        Playing = true,
                        Speed = 1,
                        Weight = 1,
                        ShouldReset = false,
                    },
                },
            };

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            world.Add(entity, pbAnimator);
        }

        protected override void OnTearDown()
        {
            sdkAnimatorUpdaterSystem.Dispose();
            legacyAnimationPlayerSystem.Dispose();
            createGltfAssetFromAssetBundleSystem.Dispose();
            finalizeGltfContainerLoadingSystem.Dispose();
            resources.UnloadBundle();
        }

        [Test]
        public async Task WriteBackFinishedLegacyClipExactlyOnce()
        {
            // Arrange: load the legacy animation asset and start playback of the non-looping "bite" clip.
            Animation animation = await StartLegacyPlaybackAsync();

            Action<PBAnimator, PBAnimator> capturedPrepare = null;
            PBAnimator capturedData = null;

            ecsToCRDTWriter.PutMessage(
                Arg.Do<Action<PBAnimator, PBAnimator>>(prepare => capturedPrepare = prepare),
                Arg.Any<CRDTEntity>(),
                Arg.Do<PBAnimator>(data => capturedData = data));

            system.Update(0); // latches ObservedPlaying while the clip is active

            // Act: simulate the clip reaching its natural end.
            animation.Stop("bite");
            system.Update(0);

            // Assert: PBAnimator mutated in place without dirtying, mirror updated, exactly one PUT.
            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBAnimator, PBAnimator>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAnimator>());

            Assert.That(pbAnimator.States[0].Playing, Is.False);
            Assert.That(pbAnimator.States[1].Playing, Is.True);
            Assert.That(pbAnimator.IsDirty, Is.False);

            SDKAnimatorComponent sdkAnimator = world.Get<SDKAnimatorComponent>(entity);
            Assert.That(sdkAnimator.IsDirty, Is.False);
            Assert.That(sdkAnimator.SDKAnimationStates[0].Playing, Is.False);
            Assert.That(sdkAnimator.SDKAnimationStates[0].ObservedPlaying, Is.False);

            // The prepare lambda shares the live PBAnimationState references into the rented message.
            var rented = new PBAnimator();
            capturedPrepare!(rented, capturedData);
            Assert.That(rented.States.Count, Is.EqualTo(2));
            Assert.That(rented.States[0], Is.SameAs(pbAnimator.States[0]));

            // Act: further updates must not repeat the writeback.
            system.Update(0);
            system.Update(0);

            // Assert
            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBAnimator, PBAnimator>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAnimator>());
        }

        [Test]
        public async Task NeverWriteBackLoopingClip()
        {
            // Arrange
            Animation animation = await StartLegacyPlaybackAsync();

            system.Update(0); // would latch, but looping states are not tracked at all

            // Act: even a manual stop of a looping clip must not be reported as a finish.
            animation.Stop("swim");
            system.Update(0);

            // Assert
            Assert.That(pbAnimator.States[1].Playing, Is.True);
            ecsToCRDTWriter.DidNotReceive().PutMessage(Arg.Any<Action<PBAnimator, PBAnimator>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAnimator>());
        }

        [Test]
        public async Task SkipDetectionWhileSceneWriteIsPending()
        {
            // Arrange
            Animation animation = await StartLegacyPlaybackAsync();
            system.Update(0); // latch

            animation.Stop("bite");

            SDKAnimatorComponent sdkAnimator = world.Get<SDKAnimatorComponent>(entity);
            sdkAnimator.IsDirty = true; // a scene write is pending; observations would be stale
            world.Set(entity, sdkAnimator);

            // Act
            system.Update(0);

            // Assert
            ecsToCRDTWriter.DidNotReceive().PutMessage(Arg.Any<Action<PBAnimator, PBAnimator>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAnimator>());
        }

        [Test]
        public async Task NotWriteBackWhenSceneStopsClipTheSameFrame()
        {
            // Arrange
            await StartLegacyPlaybackAsync();
            system.Update(0); // latch while playing

            // Act: the scene stops the clip; the incoming PUT rebuilds the states (resetting the latch)
            // before playback observation happens.
            pbAnimator.States[0].Playing = false;
            pbAnimator.IsDirty = true;
            sdkAnimatorUpdaterSystem.Update(0);
            legacyAnimationPlayerSystem.Update(0);
            system.Update(0);

            // Assert: scene-driven stop is never reported as a natural finish.
            ecsToCRDTWriter.DidNotReceive().PutMessage(Arg.Any<Action<PBAnimator, PBAnimator>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAnimator>());
        }

        [Test]
        public void LatchAndFinishFollowEdgeTriggerRules()
        {
            var states = new List<SDKAnimationState>
            {
                new (new PBAnimationState { Clip = "a", Playing = true, Loop = false }),
            };

            // Not active and never observed: a clip that has not started yet is not a finish.
            Assert.That(AnimatorFinishWritebackSystem.UpdateLatch(states, 0, isActive: false), Is.False);
            Assert.That(states[0].ObservedPlaying, Is.False);

            // Active: latch.
            Assert.That(AnimatorFinishWritebackSystem.UpdateLatch(states, 0, isActive: true), Is.False);
            Assert.That(states[0].ObservedPlaying, Is.True);

            // Still active: no change.
            Assert.That(AnimatorFinishWritebackSystem.UpdateLatch(states, 0, isActive: true), Is.False);

            // No longer active after being observed: natural finish, mirror stopped and latch reset.
            Assert.That(AnimatorFinishWritebackSystem.UpdateLatch(states, 0, isActive: false), Is.True);
            Assert.That(states[0].Playing, Is.False);
            Assert.That(states[0].ObservedPlaying, Is.False);

            // Never finishes twice.
            Assert.That(AnimatorFinishWritebackSystem.UpdateLatch(states, 0, isActive: false), Is.False);
        }

        [Test]
        public void ConsiderMecanimStateActiveOnlyWhileCursorBelowEnd()
        {
            Assert.That(AnimatorFinishWritebackSystem.IsMecanimStateActive(stateIsClip: true, normalizedTime: 0.5f), Is.True);
            Assert.That(AnimatorFinishWritebackSystem.IsMecanimStateActive(stateIsClip: true, normalizedTime: 0f), Is.True);

            // Cursor reached the end of a non-looping clip.
            Assert.That(AnimatorFinishWritebackSystem.IsMecanimStateActive(stateIsClip: true, normalizedTime: 1f), Is.False);

            // The state machine already exited the clip state.
            Assert.That(AnimatorFinishWritebackSystem.IsMecanimStateActive(stateIsClip: false, normalizedTime: 0.5f), Is.False);
        }

        [Test]
        public void TrackOnlyPlayingNonLoopingStates()
        {
            Assert.That(AnimatorFinishWritebackSystem.IsTracked(new SDKAnimationState(new PBAnimationState { Clip = "a", Playing = true, Loop = false })), Is.True);
            Assert.That(AnimatorFinishWritebackSystem.IsTracked(new SDKAnimationState(new PBAnimationState { Clip = "a", Playing = true, Loop = true })), Is.False);
            Assert.That(AnimatorFinishWritebackSystem.IsTracked(new SDKAnimationState(new PBAnimationState { Clip = "a", Playing = false, Loop = false })), Is.False);
        }

        private async Task<Animation> StartLegacyPlaybackAsync()
        {
            await InitializeGltfContainerComponentAsync();
            legacyAnimationPlayerSystem.Update(0);

            GltfContainerComponent gltfContainerComponent = world.Get<GltfContainerComponent>(entity);
            Animation animation = gltfContainerComponent.Promise.Result!.Value.Asset.Animations[0];

            Assert.That(animation.IsPlaying("bite"), Is.True);
            Assert.That(world.Get<SDKAnimatorComponent>(entity).IsDirty, Is.False);

            return animation;
        }

        private async Task InitializeGltfContainerComponentAsync()
        {
            var component = new GltfContainerComponent(ColliderLayer.ClPhysics, ColliderLayer.ClPointer,
                AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                    world, new GetGltfContainerAssetIntention(GltfContainerTestResources.ANIMATION, GltfContainerTestResources.ANIMATION, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY));

            component.State = LoadingState.Loading;

            StreamableLoadingResult<AssetBundleData> assetBundleData = await resources.LoadAssetBundle(GltfContainerTestResources.ANIMATION);

            // Just pass it through another system for simplicity, otherwise there is too much logic to replicate
            world.Add(component.Promise.Entity, assetBundleData);
            createGltfAssetFromAssetBundleSystem.Update(0);

            world.Add(entity, component, new CRDTEntity(100), new PBGltfContainer { Src = GltfContainerTestResources.ANIMATION });

            finalizeGltfContainerLoadingSystem.Update(0);
        }
    }
}
