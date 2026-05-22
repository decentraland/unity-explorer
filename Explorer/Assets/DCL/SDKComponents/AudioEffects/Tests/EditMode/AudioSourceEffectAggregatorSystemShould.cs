using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.AudioEffects.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.SDKComponents.AudioEffects.Tests
{
    [TestFixture]
    public class AudioSourceEffectAggregatorSystemShould : UnitySystemTestBase<AudioSourceEffectAggregatorSystem>
    {
        private RecordingRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            registry = new RecordingRegistry();
            system = new AudioSourceEffectAggregatorSystem(world, registry);
        }

        [Test]
        public void UpsertNewDirtySource()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            world.Create(pb);

            system!.Update(0f);

            Assert.That(registry.Upserts, Has.Count.EqualTo(1));
            Assert.That(registry.Upserts[0].Target, Is.EqualTo("0xABC"));
            Assert.That(registry.Upserts[0].Pb, Is.SameAs(pb));
            Assert.That(registry.Removes, Is.Empty);
        }

        [Test]
        public void IgnoreNonDirtySources()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = false };
            world.Create(pb);

            system!.Update(0f);

            Assert.That(registry.Upserts, Is.Empty);
            Assert.That(registry.Removes, Is.Empty);
        }

        [Test]
        public void OnlyUpsertChangedSourcesAcrossManyEntities()
        {
            var dirty = new PBAudioSourceEffect { TargetAvatarId = "0xAAA", IsDirty = true };
            var clean = new PBAudioSourceEffect { TargetAvatarId = "0xBBB", IsDirty = false };
            world.Create(dirty);
            world.Create(clean);

            system!.Update(0f);

            Assert.That(registry.Upserts, Has.Count.EqualTo(1));
            Assert.That(registry.Upserts[0].Pb, Is.SameAs(dirty));
        }

        [Test]
        public void RemoveDirtySourceWithEmptyTarget()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "", IsDirty = true };
            world.Create(pb);

            system!.Update(0f);

            Assert.That(registry.Upserts, Is.Empty);
            Assert.That(registry.Removes, Has.Count.EqualTo(1));
            Assert.That(registry.Removes[0], Is.SameAs(pb));
        }

        [Test]
        public void RemoveSourceOnDestroy()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            Entity e = world.Create(pb);

            system!.Update(0f);
            registry.Reset();

            world.Add(e, new DeleteEntityIntention());

            system!.Update(0f);

            Assert.That(registry.Upserts, Is.Empty);
            Assert.That(registry.Removes, Has.Count.EqualTo(1));
            Assert.That(registry.Removes[0], Is.SameAs(pb));
        }

        [Test]
        public void DoNotUpsertEntitiesPendingDeletionEvenIfDirty()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            Entity e = world.Create(pb);
            world.Add(e, new DeleteEntityIntention());

            system!.Update(0f);

            Assert.That(registry.Upserts, Is.Empty);
            Assert.That(registry.Removes, Has.Count.EqualTo(1));
        }

        [Test]
        public void EmitZeroCallsOnQuiescentTick()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            world.Create(pb);

            system!.Update(0f);
            pb.IsDirty = false;
            registry.Reset();

            system!.Update(0f);

            Assert.That(registry.Upserts, Is.Empty);
            Assert.That(registry.Removes, Is.Empty);
            Assert.That(registry.ClearCalls, Is.EqualTo(0));
        }

        [Test]
        public void CallRegistryClearOnFinalizeComponents()
        {
            var desc = new QueryDescription();
            Query q = world.Query(in desc);

            system!.FinalizeComponents(in q);

            Assert.That(registry.ClearCalls, Is.EqualTo(1));
        }

        private sealed class RecordingRegistry : ISceneAudioEffectsRegistry
        {
            public readonly List<UpsertCall> Upserts = new ();
            public readonly List<PBAudioSourceEffect> Removes = new ();
            public int ClearCalls;

            public void Clear() =>
                ClearCalls++;

            public void Upsert(string targetAvatarId, PBAudioSourceEffect pbEffect) =>
                Upserts.Add(new UpsertCall(targetAvatarId, pbEffect));

            public void Remove(PBAudioSourceEffect pbEffect) =>
                Removes.Add(pbEffect);

            public bool TryGetEffects(string ethAddress, out List<PBAudioSourceEffect> effects)
            {
                effects = null;
                return false;
            }

            public void Reset()
            {
                Upserts.Clear();
                Removes.Clear();
                ClearCalls = 0;
            }

            public readonly struct UpsertCall
            {
                public readonly string Target;
                public readonly PBAudioSourceEffect Pb;

                public UpsertCall(string target, PBAudioSourceEffect pb)
                {
                    Target = target;
                    Pb = pb;
                }
            }
        }
    }
}
