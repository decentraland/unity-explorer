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
            Assert.IsFalse(component.Isolated);
        }

        [Test]
        public void ResolveTheMostRestrictiveValueOfEachProperty()
        {
            var component = new VoiceChatAreaModifiersComponent(new List<VoiceChatAreaModifier>
            {
                Modifier(volumeScale: 0.4f, maxDistance: 10f, mute: false, isolate: false),
                Modifier(volumeScale: 0.8f, maxDistance: 3f, mute: true, isolate: true),
            });

            Assert.AreEqual(0.4f, component.VolumeScale);
            Assert.AreEqual(3f, component.MaxDistance);
            Assert.IsTrue(component.Muted);
            Assert.IsTrue(component.Isolated);
        }

        [Test]
        public void ResolveAgainAfterTheModifiersChange()
        {
            var component = new VoiceChatAreaModifiersComponent(new List<VoiceChatAreaModifier> { Modifier(volumeScale: 0.2f, mute: true) });

            component.Modifiers.Clear();
            component.Resolve();

            Assert.AreEqual(1f, component.VolumeScale);
            Assert.IsFalse(component.Muted);
        }

        [Test]
        public void LetListenersOutsideIsolationHearSpeakersOutsideIsolation()
        {
            Assert.IsTrue(VoiceChatAreaModifiersComponent.CanHear(Areas(), Areas()));
            Assert.IsTrue(VoiceChatAreaModifiersComponent.CanHear(Areas(Modifier(isolate: false)), Areas()));
        }

        [Test]
        public void LetListenersHearSpeakersInTheSameIsolatingArea()
        {
            VoiceChatAreaModifier booth = Modifier(isolate: true);

            Assert.IsTrue(VoiceChatAreaModifiersComponent.CanHear(Areas(booth), Areas(booth)));
        }

        [Test]
        public void BlockVoicesAcrossTheIsolationBoundary()
        {
            VoiceChatAreaModifiersComponent booth = Areas(Modifier(isolate: true));

            Assert.IsFalse(VoiceChatAreaModifiersComponent.CanHear(booth, Areas()));
            Assert.IsFalse(VoiceChatAreaModifiersComponent.CanHear(Areas(), booth));
        }

        [Test]
        public void BlockVoicesBetweenDifferentIsolatingAreas()
        {
            VoiceChatAreaModifiersComponent boothA = Areas(Modifier(isolate: true));
            VoiceChatAreaModifiersComponent boothB = Areas(Modifier(isolate: true));

            Assert.IsFalse(VoiceChatAreaModifiersComponent.CanHear(boothA, boothB));
        }

        private static VoiceChatAreaModifiersComponent Areas(params VoiceChatAreaModifier[] modifiers) =>
            new (new List<VoiceChatAreaModifier>(modifiers));

        private VoiceChatAreaModifier Modifier(float volumeScale = 1f, float maxDistance = float.PositiveInfinity, bool mute = false, bool isolate = false) =>
            new (sceneWorld, sceneWorld.Create(), volumeScale, maxDistance, mute, isolate);
    }
}
