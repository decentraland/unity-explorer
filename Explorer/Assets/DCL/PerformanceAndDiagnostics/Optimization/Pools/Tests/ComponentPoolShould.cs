using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DCL.Optimization.Pools.Tests
{
    [TestFixture]
    public class ComponentPoolShould
    {
        [SetUp]
        public void SetUp()
        {
            onRelease = Substitute.For<Action<TestClass>>();
            onGet = Substitute.For<Action<TestClass>>();
            componentPool = new ComponentPool.WithDefaultCtor<TestClass>(onGet, onRelease);
        }

        [TearDown]
        public void TearDown()
        {
            componentPool.Clear();
        }

        public class TestClass
        {
            public int Value;
        }

        private IComponentPool<TestClass> componentPool;
        private Action<TestClass> onRelease;
        private Action<TestClass> onGet;

        [Test]
        public async Task GetFromMultipleThreads()
        {
            async UniTask Run()
            {
                TestClass component = componentPool.Get();
                component.Value = 1;
                await UniTask.SwitchToThreadPool();
                await UniTask.Delay(100);
                componentPool.Release(component);
            }

            var tasks = Enumerable.Range(0, 10).Select(_ => Run()).ToList();
            await UniTask.WhenAll(tasks);

            onGet.Received(10).Invoke(Arg.Any<TestClass>());
        }

        [Test]

        // Basically checks that are no exceptions
        public async Task MixGetReleaseFromMultipleThreads([Values(5, 10, 30, 50, 100, 500, 5000)] int threadsCount)
        {
            async UniTask Run()
            {
                await UniTask.SwitchToThreadPool();
                var random = new Random();
                TestClass component = componentPool.Get();
                component.Value = 1;
                await UniTask.Delay(TimeSpan.FromTicks(100 + (int)((random.NextDouble() * 20) - 10d)));
                componentPool.Release(component);
            }

            var tasks = Enumerable.Range(0, threadsCount).Select(_ => Run()).ToList();
            await UniTask.WhenAll(tasks);

            await UniTask.SwitchToMainThread();

            onGet.Received(threadsCount).Invoke(Arg.Any<TestClass>());
            onRelease.Received(threadsCount).Invoke(Arg.Any<TestClass>());
        }
    }
}
