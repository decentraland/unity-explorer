using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using NUnit.Framework;
using System.Threading;
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
            profiler = new Profiler();
        }

        public struct DummyComponent
        {
            public int timesUpdated;
        }

        private FrameTimeCapBudget budget;
        private IBudgetProfiler profiler;

        [Test]
        public async Task SpendBudget()
        {
            budget = new FrameTimeCapBudget(5f, profiler);

            //We need to wait one frame for the Profiling Provider start getting samples
            await UniTask.Yield();

            // Assert
            Assert.AreEqual(true, budget.TrySpendBudget());

            //We are blocking the main thread
            for (var i = 0; i < 1_000; i++)
                GameObject.CreatePrimitive(PrimitiveType.Cube);

            Assert.AreEqual(false, budget.TrySpendBudget());
        }

        [Test]
        public async Task StopSystemWhenBudgetIsBlown()
        {
            budget = new FrameTimeCapBudget(15f, profiler);

            var world = World.Create();
            Entity e = world.Create(new DummyComponent());

            //We need to wait one frame for the Profiling Provider start getting samples
            await UniTask.Yield();

            world.Query(new QueryDescription().WithAll<DummyComponent>(), (ref DummyComponent dummy) => DummySystem(ref dummy));
            Assert.AreEqual(1, world.Get<DummyComponent>(e).timesUpdated);

            //We are blocking the main thread
            Thread.Sleep(15);

            world.Query(new QueryDescription().WithAll<DummyComponent>(), (ref DummyComponent dummy) => DummySystem(ref dummy));
            Assert.AreEqual(1, world.Get<DummyComponent>(e).timesUpdated);
        }

        private void DummySystem(ref DummyComponent dummyComponent)
        {
            if (budget.TrySpendBudget())
                dummyComponent.timesUpdated++;
        }
    }
}
