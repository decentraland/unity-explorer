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
        public void StoreSourceAndReturnItViaTryGetSources()
        {
            var pb = new PBAudioSourceEffect();

            registry.Upsert("0xABC", pb);

            Assert.That(registry.TryGetEffects("0xABC", out List<PBAudioSourceEffect> sources), Is.True);
            Assert.That(sources.Count, Is.EqualTo(1));
            Assert.That(sources[0], Is.SameAs(pb));
        }

        [Test]
        public void ReportMissForUnknownAddress()
        {
            Assert.That(registry.TryGetEffects("0xUNKNOWN", out _), Is.False);
        }

        [Test]
        public void MatchAddressCaseInsensitively()
        {
            var pb = new PBAudioSourceEffect();

            registry.Upsert("0xABC", pb);

            Assert.That(registry.TryGetEffects("0xabc", out List<PBAudioSourceEffect> sources), Is.True);
            Assert.That(sources[0], Is.SameAs(pb));
        }

        [Test]
        public void NoOpWhenUpsertingSamePbAndTarget()
        {
            var pb = new PBAudioSourceEffect();

            registry.Upsert("0xABC", pb);
            registry.Upsert("0xABC", pb);

            Assert.That(registry.TryGetEffects("0xABC", out List<PBAudioSourceEffect> sources), Is.True);
            Assert.That(sources.Count, Is.EqualTo(1));
            Assert.That(sources[0], Is.SameAs(pb));
        }

        [Test]
        public void IgnoreTargetChangeForAlreadyRegisteredEffect()
        {
            var pb = new PBAudioSourceEffect();

            registry.Upsert("0xAAA", pb);
            registry.Upsert("0xBBB", pb);

            Assert.That(registry.TryGetEffects("0xBBB", out _), Is.False, "second upsert must not create a new chain");
            Assert.That(registry.TryGetEffects("0xAAA", out List<PBAudioSourceEffect> sources), Is.True);
            Assert.That(sources.Count, Is.EqualTo(1));
            Assert.That(sources[0], Is.SameAs(pb));
        }

        [Test]
        public void IsolateChainsBetweenTargets()
        {
            var pbA = new PBAudioSourceEffect();
            var pbB = new PBAudioSourceEffect();

            registry.Upsert("0xAAA", pbA);
            registry.Upsert("0xBBB", pbB);

            Assert.That(registry.TryGetEffects("0xAAA", out List<PBAudioSourceEffect> sourcesA), Is.True);
            Assert.That(sourcesA.Count, Is.EqualTo(1));
            Assert.That(sourcesA[0], Is.SameAs(pbA));

            Assert.That(registry.TryGetEffects("0xBBB", out List<PBAudioSourceEffect> sourcesB), Is.True);
            Assert.That(sourcesB.Count, Is.EqualTo(1));
            Assert.That(sourcesB[0], Is.SameAs(pbB));
        }

        [Test]
        public void RemoveDropsSourceFromChain()
        {
            var pb = new PBAudioSourceEffect();
            registry.Upsert("0xABC", pb);

            registry.Remove(pb);

            Assert.That(registry.TryGetEffects("0xABC", out _), Is.False);
        }

        [Test]
        public void RemoveKeepsOtherSourcesInTheSameChain()
        {
            var pbA = new PBAudioSourceEffect();
            var pbB = new PBAudioSourceEffect();

            registry.Upsert("0xABC", pbA);
            registry.Upsert("0xABC", pbB);

            registry.Remove(pbA);

            Assert.That(registry.TryGetEffects("0xABC", out List<PBAudioSourceEffect> sources), Is.True);
            Assert.That(sources.Count, Is.EqualTo(1));
            Assert.That(sources[0], Is.SameAs(pbB));
        }

        [Test]
        public void RemoveIsNoOpForUnknownPb()
        {
            Assert.DoesNotThrow(() => registry.Remove(new PBAudioSourceEffect()));
        }

        [Test]
        public void ClearDropsAllChains()
        {
            registry.Upsert("0xAAA", new PBAudioSourceEffect());
            registry.Upsert("0xBBB", new PBAudioSourceEffect());

            registry.Clear();

            Assert.That(registry.TryGetEffects("0xAAA", out _), Is.False);
            Assert.That(registry.TryGetEffects("0xBBB", out _), Is.False);
        }

        [Test]
        public void UpsertAfterClearStartsFreshChain()
        {
            var pb = new PBAudioSourceEffect();
            registry.Upsert("0xABC", pb);

            registry.Clear();
            registry.Upsert("0xABC", pb);

            Assert.That(registry.TryGetEffects("0xABC", out List<PBAudioSourceEffect> sources), Is.True);
            Assert.That(sources.Count, Is.EqualTo(1));
            Assert.That(sources[0], Is.SameAs(pb));
        }
    }
}
