using Arch.Core;
using DCL.Audio.Systems;
using DCL.ECSComponents;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Audio.Tests
{
    public class PointerInputAudioSystemShould : UnitySystemTestBase<PointerInputAudioSystem>
    {
        private IPointerInputAudioConfigs configs;
        private AudioClipConfig primaryAudio;

        [SetUp]
        public void SetUp()
        {
            primaryAudio = ScriptableObject.CreateInstance<AudioClipConfig>();

            configs = Substitute.For<IPointerInputAudioConfigs>();
            configs.PrimaryAudio.Returns(primaryAudio);

            system = new PointerInputAudioSystem(world, configs);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(primaryAudio);
        }

        [Test]
        public void NotThrowWhenPetDownEntryHasNoEventInfo()
        {
            var sdkEvents = new PBPointerEvents
            {
                PointerEvents =
                {
                    new PBPointerEvents.Types.Entry { EventType = PointerEventType.PetDown }, // EventInfo deliberately unset (null)
                },
            };

            sdkEvents.AppendPointerEventResultsIntent.InitializeWithAlloc();
            sdkEvents.AppendPointerEventResultsIntent.AddValidIndex(0);

            world.Create(sdkEvents);

            Assert.DoesNotThrow(() => system.Update(0));
        }

        [Test]
        public void PlayAudioForPetDownWithEventInfo()
        {
            var sdkEvents = new PBPointerEvents
            {
                PointerEvents =
                {
                    new PBPointerEvents.Types.Entry
                    {
                        EventType = PointerEventType.PetDown,
                        EventInfo = new PBPointerEvents.Types.Info { Button = InputAction.IaPrimary },
                    },
                },
            };

            sdkEvents.AppendPointerEventResultsIntent.InitializeWithAlloc();
            sdkEvents.AppendPointerEventResultsIntent.AddValidIndex(0);

            world.Create(sdkEvents);

            AudioClipConfig played = null;
            void OnPlay(AudioClipConfig config, float volume) => played = config;

            UIAudioEventsBus.Instance.PlayUIAudioEvent += OnPlay;

            try { system.Update(0); }
            finally { UIAudioEventsBus.Instance.PlayUIAudioEvent -= OnPlay; }

            Assert.That(played, Is.SameAs(primaryAudio));
        }
    }
}
