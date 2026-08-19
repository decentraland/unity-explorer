using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.MediaStream;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine;
using static DCL.SDKComponents.AudioSources.Tests.AudioSourceTestsUtils;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources.Tests
{
    public class AudioEventsSystemShould : UnitySystemTestBase<AudioEventsSystem>
    {
        private const int SDK_ENTITY_ID = 512;

        private IECSToCRDTWriter ecsToCRDTWriter;
        private GameObject audioSourceGameObject;

        [SetUp]
        public void SetUp()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IPerformanceBudget budget = Substitute.For<IPerformanceBudget>();
            budget.TrySpendBudget().Returns(true);

            ISceneStateProvider sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.TickNumber.Returns(1u);

            system = new AudioEventsSystem(world, ecsToCRDTWriter, sceneStateProvider, budget);
        }

        protected override void OnTearDown()
        {
            if (audioSourceGameObject != null)
                UnityEngine.Object.DestroyImmediate(audioSourceGameObject);
        }

        [Test]
        public void WriteBackNaturalFinishExactlyOnce()
        {
            // Arrange: sdk still claims Playing while Unity playback already stopped (natural finish).
            PBAudioSource pbAudioSource = CreatePBAudioSource();
            pbAudioSource.CurrentTime = 0.75f;
            pbAudioSource.Global = true;
            Entity entity = CreateLoadedAudioSourceEntity(pbAudioSource, MediaState.MsPlaying);

            Action<PBAudioSource, PBAudioSource> capturedPrepare = null;
            PBAudioSource capturedData = null;

            ecsToCRDTWriter.PutMessage(
                Arg.Do<Action<PBAudioSource, PBAudioSource>>(prepare => capturedPrepare = prepare),
                Arg.Any<CRDTEntity>(),
                Arg.Do<PBAudioSource>(data => capturedData = data));

            // Act
            system.Update(0);

            // Assert: exactly one PUT, world instance flipped to Playing == false without dirtying it.
            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBAudioSource, PBAudioSource>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAudioSource>());

            PBAudioSource worldInstance = world.Get<PBAudioSource>(entity);
            Assert.That(worldInstance.Playing, Is.False);
            Assert.That(worldInstance.IsDirty, Is.False);

            // The prepare lambda must copy every field into the (cleared) rented message.
            var rented = new PBAudioSource();
            capturedPrepare!(rented, capturedData);
            Assert.That(rented.Playing, Is.False);
            Assert.That(rented.Volume, Is.EqualTo(0.5f));
            Assert.That(rented.Loop, Is.False);
            Assert.That(rented.Pitch, Is.EqualTo(0.5f));
            Assert.That(rented.AudioClipUrl, Is.EqualTo(worldInstance.AudioClipUrl));
            Assert.That(rented.CurrentTime, Is.EqualTo(0.75f));
            Assert.That(rented.Global, Is.True);

            // Act: second update — state is unchanged (MsReady), the edge was already consumed.
            system.Update(0);

            // Assert: no further PUT and no event spam.
            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBAudioSource, PBAudioSource>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAudioSource>());
            ecsToCRDTWriter.Received(1).AppendMessage(Arg.Any<Action<PBAudioEvent, (MediaState, uint)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<(MediaState, uint)>());
        }

        [Test]
        public void NotWriteBackWhenSceneStoppedPlayback()
        {
            // Arrange: an incoming scene PUT already flipped Playing to false before this system runs.
            PBAudioSource pbAudioSource = CreatePBAudioSource();
            pbAudioSource.Playing = false;
            CreateLoadedAudioSourceEntity(pbAudioSource, MediaState.MsPlaying);

            // Act
            system.Update(0);

            // Assert: the MsReady event is still propagated, but no writeback happens.
            ecsToCRDTWriter.Received(1).AppendMessage(Arg.Any<Action<PBAudioEvent, (MediaState, uint)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<(MediaState, uint)>());
            ecsToCRDTWriter.DidNotReceive().PutMessage(Arg.Any<Action<PBAudioSource, PBAudioSource>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAudioSource>());
        }

        [Test]
        public void NotWriteBackLoopingAudioSource()
        {
            // Arrange
            PBAudioSource pbAudioSource = CreatePBAudioSource();
            pbAudioSource.Loop = true;
            CreateLoadedAudioSourceEntity(pbAudioSource, MediaState.MsPlaying);

            // Act
            system.Update(0);

            // Assert
            ecsToCRDTWriter.DidNotReceive().PutMessage(Arg.Any<Action<PBAudioSource, PBAudioSource>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAudioSource>());
        }

        [Test]
        public void ReportErrorWhenClipLoadingFailed()
        {
            // Arrange: a consumed promise that failed keeps its Result, which must surface as MsError.
            PBAudioSource pbAudioSource = CreatePBAudioSource();
            var promise = Promise.Create(world, new GetAudioClipIntention(), PartitionComponent.TOP_PRIORITY);
            world.Add(promise.Entity, new StreamableLoadingResult<AudioClipData>(ReportCategory.SDK_AUDIO_SOURCES, new Exception("load failed")));

            var component = new AudioSourceComponent(promise, pbAudioSource.AudioClipUrl);
            component.ClipPromise.TryConsume(world, out _);

            Entity entity = world.Create(new CRDTEntity(SDK_ENTITY_ID), pbAudioSource, component);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Get<AudioSourceComponent>(entity).LastPropagatedAudioState, Is.EqualTo(MediaState.MsError));
            ecsToCRDTWriter.Received(1).AppendMessage(
                Arg.Any<Action<PBAudioEvent, (MediaState, uint)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(),
                Arg.Is<(MediaState state, uint timestamp)>(data => data.state == MediaState.MsError));
        }

        [Test]
        public void ReportErrorWhenConsumedPromiseProducedNoClip()
        {
            // Arrange: promise consumed with no retained result and no clip while a URL is set
            // (previously reported MsLoading forever).
            PBAudioSource pbAudioSource = CreatePBAudioSource();
            var component = new AudioSourceComponent(Promise.NULL, pbAudioSource.AudioClipUrl);

            Entity entity = world.Create(new CRDTEntity(SDK_ENTITY_ID), pbAudioSource, component);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Get<AudioSourceComponent>(entity).LastPropagatedAudioState, Is.EqualTo(MediaState.MsError));
        }

        [Test]
        public void NotEmitAudioEventForVideoPlayerOnlyEntity()
        {
            // Arrange: CreateMediaPlayerSystem also creates MediaPlayerComponent for PBVideoPlayer entities;
            // those must not receive PBAudioEvent (VideoEventsSystem owns them).
            var videoMediaPlayer = new MediaPlayerComponent { LastReportedMediaState = MediaState.MsPlaying };
            world.Create(new CRDTEntity(SDK_ENTITY_ID), videoMediaPlayer, new PBVideoPlayer());

            var streamMediaPlayer = new MediaPlayerComponent { LastReportedMediaState = MediaState.MsPlaying };
            var streamCrdtEntity = new CRDTEntity(SDK_ENTITY_ID + 1);
            world.Create(streamCrdtEntity, streamMediaPlayer, new PBAudioStream());

            // Act
            system.Update(0);

            // Assert: only the PBAudioStream entity got an audio event.
            ecsToCRDTWriter.Received(1).AppendMessage(Arg.Any<Action<PBAudioEvent, (MediaState, uint)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<(MediaState, uint)>());
            ecsToCRDTWriter.Received(1).AppendMessage(Arg.Any<Action<PBAudioEvent, (MediaState, uint)>>(), streamCrdtEntity, Arg.Any<int>(), Arg.Any<(MediaState, uint)>());
        }

        [Test]
        public void NotEmitEventsWhenStateIsUnchanged()
        {
            // Arrange: already propagated MsReady, playback state does not change.
            PBAudioSource pbAudioSource = CreatePBAudioSource();
            pbAudioSource.Playing = false;
            CreateLoadedAudioSourceEntity(pbAudioSource, MediaState.MsReady);

            // Act
            system.Update(0);
            system.Update(0);
            system.Update(0);

            // Assert
            ecsToCRDTWriter.DidNotReceive().AppendMessage(Arg.Any<Action<PBAudioEvent, (MediaState, uint)>>(), Arg.Any<CRDTEntity>(), Arg.Any<int>(), Arg.Any<(MediaState, uint)>());
            ecsToCRDTWriter.DidNotReceive().PutMessage(Arg.Any<Action<PBAudioSource, PBAudioSource>>(), Arg.Any<CRDTEntity>(), Arg.Any<PBAudioSource>());
        }

        [Test]
        public void DetectNaturalFinishOnlyOnPlayingToReadyTransitionOfNonLoopingPlayingSource()
        {
            var playing = new PBAudioSource { Playing = true, Loop = false };
            var stopped = new PBAudioSource { Playing = false };
            var looping = new PBAudioSource { Playing = true, Loop = true };
            var noPlayingField = new PBAudioSource();

            Assert.That(AudioEventsSystem.IsNaturalFinish(MediaState.MsPlaying, MediaState.MsReady, playing), Is.True);
            Assert.That(AudioEventsSystem.IsNaturalFinish(MediaState.MsPlaying, MediaState.MsReady, stopped), Is.False);
            Assert.That(AudioEventsSystem.IsNaturalFinish(MediaState.MsPlaying, MediaState.MsReady, looping), Is.False);
            Assert.That(AudioEventsSystem.IsNaturalFinish(MediaState.MsPlaying, MediaState.MsReady, noPlayingField), Is.False);
            Assert.That(AudioEventsSystem.IsNaturalFinish(MediaState.MsLoading, MediaState.MsReady, playing), Is.False);
            Assert.That(AudioEventsSystem.IsNaturalFinish(MediaState.MsPlaying, MediaState.MsError, playing), Is.False);
        }

        private Entity CreateLoadedAudioSourceEntity(PBAudioSource pbAudioSource, MediaState lastPropagatedState)
        {
            var promise = Promise.Create(world, new GetAudioClipIntention(), PartitionComponent.TOP_PRIORITY);
            world.Add(promise.Entity, new StreamableLoadingResult<AudioClipData>(new AudioClipData(TestAudioClip)));

            var component = new AudioSourceComponent(promise, pbAudioSource.AudioClipUrl);
            component.ClipPromise.TryConsume(world, out _);

            audioSourceGameObject = new GameObject("audio-events-test");
            AudioSource audioSource = audioSourceGameObject.AddComponent<AudioSource>();
            audioSource.clip = TestAudioClip;
            component.SetAudioSource(audioSource, null);

            component.LastPropagatedAudioState = lastPropagatedState;

            return world.Create(new CRDTEntity(SDK_ENTITY_ID), pbAudioSource, component);
        }
    }
}
