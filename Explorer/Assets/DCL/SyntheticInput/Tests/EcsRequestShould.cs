using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.SyntheticInput.Core;
using NUnit.Framework;

namespace DCL.SyntheticInput.Tests
{
    public class EcsRequestShould
    {
        private struct TestEcsRequest : IEcsRequest<int>
        {
            public int Payload;
            public UniTaskCompletionSource<int>? Completion { get; set; }
        }

        private World world = null!;
        private Entity entity;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            entity = world.Create();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void InstallRequestWithPendingCompletion()
        {
            UniTask<int> task = EcsRequest.SendAsync(world, entity, new TestEcsRequest { Payload = 7 }, -1);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.TryGet(entity, out TestEcsRequest installed), Is.True);
            Assert.That(installed.Payload, Is.EqualTo(7));
            Assert.That(installed.Completion, Is.Not.Null);
        }

        [Test]
        public void PreemptPendingRequestWhenNewerOneIsSent()
        {
            UniTask<int> first = EcsRequest.SendAsync(world, entity, new TestEcsRequest { Payload = 1 }, -1);
            UniTask<int> second = EcsRequest.SendAsync(world, entity, new TestEcsRequest { Payload = 2 }, -1);

            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(first.GetAwaiter().GetResult(), Is.EqualTo(-1));
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.Get<TestEcsRequest>(entity).Payload, Is.EqualTo(2));
        }

        [Test]
        public void CompleteAndRemoveResolvesAwaiterAfterRemoval()
        {
            UniTask<int> task = EcsRequest.SendAsync(world, entity, new TestEcsRequest(), -1);
            TestEcsRequest ecsRequest = world.Get<TestEcsRequest>(entity);

            EcsRequest.CompleteAndRemove(world, entity, ecsRequest, 42);

            Assert.That(world.Has<TestEcsRequest>(entity), Is.False);
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(task.GetAwaiter().GetResult(), Is.EqualTo(42));
        }

        [Test]
        public void CompleteAndRemoveTolerateMissingCompletion()
        {
            world.Add(entity, new TestEcsRequest());

            EcsRequest.CompleteAndRemove(world, entity, world.Get<TestEcsRequest>(entity), 0);

            Assert.That(world.Has<TestEcsRequest>(entity), Is.False);
        }

        /// <summary>
        ///     An abandon is the driver-side timeout: the driver has already given up on the awaiter, so the request
        ///     is dropped silently. Unlike <see cref="EcsRequest.CompleteAndRemove{TIntent,TResult}" /> it never touches the completion source.
        /// </summary>
        [Test]
        public void AbandonDropsTheRequestWithoutResolvingItsAwaiter()
        {
            UniTask<int> task = EcsRequest.SendAsync(world, entity, new TestEcsRequest(), -1);

            UniTask abandon = EcsRequest.AbandonAsync<TestEcsRequest>(world, entity);

            Assert.That(abandon.Status, Is.EqualTo(UniTaskStatus.Succeeded), "called from the main thread, the abandon completes synchronously");
            Assert.That(world.Has<TestEcsRequest>(entity), Is.False);
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public void AbandonIsANoOpWhenTheRequestWasAlreadyCompleted()
        {
            UniTask<int> task = EcsRequest.SendAsync(world, entity, new TestEcsRequest(), -1);
            EcsRequest.CompleteAndRemove(world, entity, world.Get<TestEcsRequest>(entity), 42);

            // The timeout losing the race against the fulfilling system is the expected shape of this call.
            UniTask abandon = EcsRequest.AbandonAsync<TestEcsRequest>(world, entity);

            Assert.That(abandon.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(world.Has<TestEcsRequest>(entity), Is.False);
            Assert.That(task.GetAwaiter().GetResult(), Is.EqualTo(42), "the result the system delivered stands");
        }

        [Test]
        public void AbandonTolerateRepeatedCalls()
        {
            world.Add(entity, new TestEcsRequest());

            UniTask first = EcsRequest.AbandonAsync<TestEcsRequest>(world, entity);
            UniTask second = EcsRequest.AbandonAsync<TestEcsRequest>(world, entity);

            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(world.Has<TestEcsRequest>(entity), Is.False);
        }
    }
}
