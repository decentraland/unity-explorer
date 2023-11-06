using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.PerformanceBudgeting;
using DCL.Profiling;
using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;

namespace ECS.StreamableLoading.Tests
{
    [TestFixture]
    public class FrameTimeCapBudgetProviderShould
    {
        [SetUp]
        public void SetUp()
        {
            profilingProvider = new ProfilingProvider();
        }

        public struct DummyComponent
        {
            public int timesUpdated;
        }

        private FrameTimeCapBudgetProvider budgetProvider;
        private ProfilingProvider profilingProvider;

        [Test]
        public async Task SpendBudget()
        {
            budgetProvider = new FrameTimeCapBudgetProvider(5f, profilingProvider);

            //We need to wait one frame for the Profiling Provider start getting samples
            await UniTask.Yield();

            // Assert
            Assert.AreEqual(true, budgetProvider.TrySpendBudget());

            //We are blocking the main thread
            for (var i = 0; i < 1_000; i++)
                GameObject.CreatePrimitive(PrimitiveType.Cube);

            Assert.AreEqual(false, budgetProvider.TrySpendBudget());
        }

        [Test]
        public async Task StopSystemWhenBudgetIsBlown()
        {
            budgetProvider = new FrameTimeCapBudgetProvider(15f, profilingProvider);

            var world = World.Create();
            Entity e = world.Create(new DummyComponent());

            //We need to wait one frame for the Profiling Provider start getting samples
            await UniTask.Yield();

            world.Query(new QueryDescription().WithAll<DummyComponent>(), (ref DummyComponent dummy) => DummySystem(ref dummy));
            Assert.AreEqual(1, world.Get<DummyComponent>(e).timesUpdated);

            //We are blocking the main thread
            for (var i = 0; i < 1_000; i++)
                GameObject.CreatePrimitive(PrimitiveType.Cube);

            world.Query(new QueryDescription().WithAll<DummyComponent>(), (ref DummyComponent dummy) => DummySystem(ref dummy));
            Assert.AreEqual(1, world.Get<DummyComponent>(e).timesUpdated);
        }

        private void DummySystem(ref DummyComponent dummyComponent)
        {
            if (budgetProvider.TrySpendBudget())
                dummyComponent.timesUpdated++;
        }
    }
}
