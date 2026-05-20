using DCL.ECSComponents;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.SDKComponents.AudioEffects.Tests
{
    [TestFixture]
    public class SceneAudioEffectsRegistryShould
    {
        private SceneAudioEffectsRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            registry = new SceneAudioEffectsRegistry();
        }

        [Test]
        public void StoreSourcesAndReturnThemViaTryGetSources()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "0xABC" };
            var sources = new[] { pb };

            registry.SetSources("0xABC", sources, silenced: false);

            Assert.That(registry.TryGetSources("0xABC", out AudioEffectSourcesSnapshot snapshot), Is.True);
            Assert.That(snapshot.Sources.Count, Is.EqualTo(1));
            Assert.That(snapshot.Sources[0], Is.SameAs(pb));
            Assert.That(snapshot.Silenced, Is.False);
        }

        [Test]
        public void DropEntryOnRemoveSources()
        {
            registry.SetSources("0xABC", new[] { new PBAudioSourceEffect() }, silenced: false);

            registry.RemoveSources("0xABC");

            Assert.That(registry.TryGetSources("0xABC", out _), Is.False);
        }

        [Test]
        public void NoOpOnRemoveOfUnknownKey()
        {
            Assert.DoesNotThrow(() => registry.RemoveSources("0xUNKNOWN"));
        }

        [Test]
        public void MatchAddressCaseInsensitively()
        {
            var pb = new PBAudioSourceEffect();

            registry.SetSources("0xABC", new[] { pb }, silenced: false);

            Assert.That(registry.TryGetSources("0xabc", out AudioEffectSourcesSnapshot snapshot), Is.True);
            Assert.That(snapshot.Sources[0], Is.SameAs(pb));
        }

        [Test]
        public void ReuseBackingListWhenSettingSameKeyAgain()
        {
            var first = new[] { new PBAudioSourceEffect() };
            registry.SetSources("0xABC", first, silenced: false);
            Assert.That(registry.TryGetSources("0xABC", out AudioEffectSourcesSnapshot firstSnapshot), Is.True);
            IReadOnlyList<PBAudioSourceEffect> firstList = firstSnapshot.Sources;

            var second = new[] { new PBAudioSourceEffect(), new PBAudioSourceEffect() };
            registry.SetSources("0xABC", second, silenced: true);
            Assert.That(registry.TryGetSources("0xABC", out AudioEffectSourcesSnapshot secondSnapshot), Is.True);

            Assert.That(secondSnapshot.Sources, Is.SameAs(firstList), "the registry must reuse the same backing list");
            Assert.That(secondSnapshot.Sources.Count, Is.EqualTo(2));
            Assert.That(secondSnapshot.Silenced, Is.True);
        }

        [Test]
        public void ClearAllEntriesAndReusePoolAfterwards()
        {
            var firstSources = new[] { new PBAudioSourceEffect() };
            registry.SetSources("0xABC", firstSources, silenced: true);
            Assert.That(registry.TryGetSources("0xABC", out AudioEffectSourcesSnapshot firstSnapshot), Is.True);
            IReadOnlyList<PBAudioSourceEffect> firstList = firstSnapshot.Sources;

            registry.Clear();

            Assert.That(registry.TryGetSources("0xABC", out _), Is.False);

            registry.SetSources("0xABC", firstSources, silenced: false);
            Assert.That(registry.TryGetSources("0xABC", out AudioEffectSourcesSnapshot afterClear), Is.True);
            Assert.That(afterClear.Sources, Is.SameAs(firstList));
        }

        [Test]
        public void PreserveSilencedFlagIndependentlyOfSources()
        {
            var pb = new PBAudioSourceEffect();
            registry.SetSources("0xABC", new[] { pb }, silenced: true);

            Assert.That(registry.TryGetSources("0xABC", out AudioEffectSourcesSnapshot snapshot), Is.True);
            Assert.That(snapshot.Sources.Count, Is.EqualTo(1));
            Assert.That(snapshot.Silenced, Is.True);
        }
    }
}
