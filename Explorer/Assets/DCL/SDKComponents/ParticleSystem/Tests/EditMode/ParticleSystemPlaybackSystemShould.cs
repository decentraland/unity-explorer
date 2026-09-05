using DCL.ECSComponents;
using DCL.SDKComponents.ParticleSystem;
using DCL.SDKComponents.ParticleSystem.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine;

namespace DCL.ParticleSystem.Tests
{
    public class ParticleSystemPlaybackSystemShould : UnitySystemTestBase<ParticleSystemPlaybackSystem>
    {
        private GameObject testGameObject;
        private UnityEngine.ParticleSystem testParticleSystem;

        [SetUp]
        public void SetUp()
        {
            testGameObject = new GameObject("TestPS");
            testParticleSystem = testGameObject.AddComponent<UnityEngine.ParticleSystem>();
            system = new ParticleSystemPlaybackSystem(world);
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
        }

        [Test]
        public void PlayWhenPlaybackStatePlaying()
        {
            testParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                PlaybackState = PBParticleSystem.Types.PlaybackState.PsPlaying
            };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isPlaying);
        }

        [Test]
        public void PauseWhenPlaybackStatePaused()
        {
            testParticleSystem.Play();

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                PlaybackState = PBParticleSystem.Types.PlaybackState.PsPaused
            };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isPaused);
        }

        [Test]
        public void StopWhenPlaybackStateStopped()
        {
            testParticleSystem.Play();

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem
            {
                IsDirty = true,
                PlaybackState = PBParticleSystem.Types.PlaybackState.PsStopped
            };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isStopped);
        }

        [Test]
        public void DefaultToPlayingWhenPlaybackStateNotSet()
        {
            testParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem { IsDirty = true };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isPlaying);
        }

        [Test]
        public void StopAllOnSceneNotCurrent()
        {
            testParticleSystem.Play();

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem { PlaybackState = PBParticleSystem.Types.PlaybackState.PsPlaying };

            world.Create(pb, component);
            system.OnSceneIsCurrentChanged(false);

            Assert.IsTrue(testParticleSystem.isStopped);
        }

        [Test]
        public void ResumePlayingOnSceneCurrent()
        {
            testParticleSystem.Stop();

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem { PlaybackState = PBParticleSystem.Types.PlaybackState.PsPlaying };

            world.Create(pb, component);
            system.OnSceneIsCurrentChanged(true);

            Assert.IsTrue(testParticleSystem.isPlaying);
        }

        [Test]
        public void NotResumeStoppedOnSceneCurrent()
        {
            testParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem { PlaybackState = PBParticleSystem.Types.PlaybackState.PsStopped };

            world.Create(pb, component);
            system.OnSceneIsCurrentChanged(true);

            Assert.IsTrue(testParticleSystem.isStopped);
        }

        [Test]
        public void SkipPlaybackWhenNotDirtyAndNoRestart()
        {
            testParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var component = new ParticleSystemComponent(testParticleSystem, testGameObject);
            var pb = new PBParticleSystem
            {
                IsDirty = false,
                PlaybackState = PBParticleSystem.Types.PlaybackState.PsPlaying
            };

            world.Create(pb, component);
            system.Update(0);

            Assert.IsTrue(testParticleSystem.isStopped);
        }
    }
}
