using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.SDKComponents.AudioEffects.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System;
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
        public void WriteOneSetSourcesForOneSourceTargetingOneAvatar()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            world.Create(new CRDTEntity(512), pb);

            system!.Update(0f);

            Assert.That(registry.SetCalls, Has.Count.EqualTo(1));
            Assert.That(registry.SetCalls[0].Address, Is.EqualTo("0xABC").IgnoreCase);
            Assert.That(registry.SetCalls[0].Sources, Is.EqualTo(new[] { pb }));
            Assert.That(registry.SetCalls[0].Silenced, Is.False);
        }

        [Test]
        public void EmitOneSetSourcesPerDistinctTarget()
        {
            var pbA = new PBAudioSourceEffect { TargetAvatarId = "0xAAA", IsDirty = true };
            var pbB = new PBAudioSourceEffect { TargetAvatarId = "0xBBB", IsDirty = true };
            world.Create(new CRDTEntity(1), pbA);
            world.Create(new CRDTEntity(2), pbB);

            system!.Update(0f);

            Assert.That(registry.SetCalls, Has.Count.EqualTo(2));
            Assert.That(registry.SetCalls.Exists(c => c.Address.Equals("0xAAA", StringComparison.OrdinalIgnoreCase) && c.Sources.Length == 1 && c.Sources[0] == pbA && !c.Silenced));
            Assert.That(registry.SetCalls.Exists(c => c.Address.Equals("0xBBB", StringComparison.OrdinalIgnoreCase) && c.Sources.Length == 1 && c.Sources[0] == pbB && !c.Silenced));
        }

        [Test]
        public void StackSameTargetSortedByCrdtIdAscendingRegardlessOfInsertionOrder()
        {
            var pbHigh = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            var pbLow = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            world.Create(new CRDTEntity(999), pbHigh);
            world.Create(new CRDTEntity(100), pbLow);

            system!.Update(0f);

            Assert.That(registry.SetCalls, Has.Count.EqualTo(1));
            Assert.That(registry.SetCalls[0].Sources, Is.EqualTo(new[] { pbLow, pbHigh }));
            Assert.That(registry.SetCalls[0].Silenced, Is.False);
        }

        [Test]
        public void SetSilencedTrueIfAnySourceInChainHasSilence()
        {
            var pbA = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            var pbB = new PBAudioSourceEffect { TargetAvatarId = "0xABC", Silence = true, IsDirty = true };
            world.Create(new CRDTEntity(1), pbA);
            world.Create(new CRDTEntity(2), pbB);

            system!.Update(0f);

            Assert.That(registry.SetCalls, Has.Count.EqualTo(1));
            Assert.That(registry.SetCalls[0].Silenced, Is.True);
        }

        [Test]
        public void SkipSourcesWithEmptyTargetAvatarId()
        {
            var inert = new PBAudioSourceEffect { TargetAvatarId = "", IsDirty = true };
            var live = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            world.Create(new CRDTEntity(1), inert);
            world.Create(new CRDTEntity(2), live);

            system!.Update(0f);

            Assert.That(registry.SetCalls, Has.Count.EqualTo(1));
            Assert.That(registry.SetCalls[0].Address, Is.EqualTo("0xABC").IgnoreCase);
            Assert.That(registry.SetCalls[0].Sources, Is.EqualTo(new[] { live }));
        }

        [Test]
        public void RemoveSourcesForAddressWhoseLastSourceIsBeingDestroyed()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            Entity e = world.Create(new CRDTEntity(1), pb);

            system!.Update(0f);
            registry.Reset();

            world.Add(e, new DeleteEntityIntention());

            system!.Update(0f);

            Assert.That(registry.SetCalls, Is.Empty);
            Assert.That(registry.RemoveCalls, Has.Count.EqualTo(1));
            Assert.That(registry.RemoveCalls[0], Is.EqualTo("0xABC").IgnoreCase);
        }

        [Test]
        public void EmitZeroRegistryCallsOnQuiescentTick()
        {
            var pb = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            world.Create(new CRDTEntity(1), pb);

            system!.Update(0f);
            pb.IsDirty = false;
            registry.Reset();

            system!.Update(0f);

            Assert.That(registry.SetCalls, Is.Empty);
            Assert.That(registry.RemoveCalls, Is.Empty);
            Assert.That(registry.ClearCalls, Is.EqualTo(0));
        }

        [Test]
        public void GroupDifferentlyCasedAddressesIntoSingleChain()
        {
            var pbA = new PBAudioSourceEffect { TargetAvatarId = "0xABC", IsDirty = true };
            var pbB = new PBAudioSourceEffect { TargetAvatarId = "0xabc", IsDirty = true };
            world.Create(new CRDTEntity(1), pbA);
            world.Create(new CRDTEntity(2), pbB);

            system!.Update(0f);

            Assert.That(registry.SetCalls, Has.Count.EqualTo(1));
            Assert.That(registry.SetCalls[0].Sources, Is.EqualTo(new[] { pbA, pbB }));
            Assert.That(registry.SetCalls[0].Silenced, Is.False);
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
            public readonly List<SetCall> SetCalls = new ();
            public readonly List<string> RemoveCalls = new ();
            public int ClearCalls;

            public void SetSources(string ethAddress, IReadOnlyList<PBAudioSourceEffect> sortedSources, bool silenced)
            {
                var copy = new PBAudioSourceEffect[sortedSources.Count];
                for (var i = 0; i < sortedSources.Count; i++)
                    copy[i] = sortedSources[i];

                SetCalls.Add(new SetCall(ethAddress, copy, silenced));
            }

            public void RemoveSources(string ethAddress) =>
                RemoveCalls.Add(ethAddress);

            public bool TryGetSources(string ethAddress, out AudioEffectSourcesSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }

            public void Clear() =>
                ClearCalls++;

            public void Reset()
            {
                SetCalls.Clear();
                RemoveCalls.Clear();
                ClearCalls = 0;
            }

            public readonly struct SetCall
            {
                public readonly string Address;
                public readonly PBAudioSourceEffect[] Sources;
                public readonly bool Silenced;

                public SetCall(string address, PBAudioSourceEffect[] sources, bool silenced)
                {
                    Address = address;
                    Sources = sources;
                    Silenced = silenced;
                }
            }
        }
    }
}
