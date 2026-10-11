using Arch.Core;
using DCL.VoiceChat.Nearby;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.VoiceChat.NearbyVoiceChat.Tests.EditMode
{
    public class VoiceChatAreaModifiersComponentShould
    {
        private World sceneWorld = null!;

        [SetUp]
        public void SetUp()
        {
            sceneWorld = World.Create();
        }

        [TearDown]
        public void TearDown()
        {
            World.Destroy(sceneWorld);
        }

        [Test]
        public void ResolveDefaultsWithoutModifiers()
        {
            var component = new VoiceChatAreaModifiersComponent(new List<VoiceChatAreaModifier>());

            Assert.AreEqual(1f, component.VolumeScale);
            Assert.AreEqual(float.PositiveInfinity, component.MaxDistance);
            Assert.IsFalse(component.Muted);
        }

        [Test]
        public void ResolveTheMostRestrictiveValueOfEachProperty()
        {
            var component = new VoiceChatAreaModifiersComponent(new List<VoiceChatAreaModifier>
            {
                Modifier(volumeScale: 0.4f, maxDistance: 10f, mute: false, isolate: false),
                Modifier(volumeScale: 0.8f, maxDistance: 3f, mute: true, isolate: false),
            });

            Assert.AreEqual(0.4f, component.VolumeScale);
            Assert.AreEqual(3f, component.MaxDistance);
            Assert.IsTrue(component.Muted);
        }

        [Test]
        public void LetListenersOutsideIsolationHearSpeakersOutsideIsolation()
        {
            Assert.IsTrue(VoiceChatAreaModifiersComponent.CanHear(null, null));
            Assert.IsTrue(VoiceChatAreaModifiersComponent.CanHear(new List<VoiceChatAreaModifier> { Modifier(isolate: false) }, null));
        }

        [Test]
        public void LetListenersHearSpeakersInTheSameIsolatingArea()
        {
            VoiceChatAreaModifier booth = Modifier(isolate: true);

            Assert.IsTrue(VoiceChatAreaModifiersComponent.CanHear(new List<VoiceChatAreaModifier> { booth }, new List<VoiceChatAreaModifier> { booth }));
        }

        [Test]
        public void BlockVoicesAcrossTheIsolationBoundary()
        {
            var booth = new List<VoiceChatAreaModifier> { Modifier(isolate: true) };

            Assert.IsFalse(VoiceChatAreaModifiersComponent.CanHear(booth, null));
            Assert.IsFalse(VoiceChatAreaModifiersComponent.CanHear(null, booth));
        }

        [Test]
        public void BlockVoicesBetweenDifferentIsolatingAreas()
        {
            var boothA = new List<VoiceChatAreaModifier> { Modifier(isolate: true) };
            var boothB = new List<VoiceChatAreaModifier> { Modifier(isolate: true) };

            Assert.IsFalse(VoiceChatAreaModifiersComponent.CanHear(boothA, boothB));
        }

        private VoiceChatAreaModifier Modifier(float volumeScale = 1f, float maxDistance = float.PositiveInfinity, bool mute = false, bool isolate = false) =>
            new (sceneWorld, sceneWorld.Create(), volumeScale, maxDistance, mute, isolate);
    }
}
