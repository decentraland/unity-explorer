using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using NUnit.Framework;

namespace DCL.McpServer.Tests
{
    public class McpEcsRequestShould
    {
        private struct TestEcsRequest : IMcpEcsRequest<int>
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
            UniTask<int> task = McpEcsRequest.SendAsync(world, entity, new TestEcsRequest { Payload = 7 }, -1);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.TryGet(entity, out TestEcsRequest installed), Is.True);
            Assert.That(installed.Payload, Is.EqualTo(7));
            Assert.That(installed.Completion, Is.Not.Null);
        }

        [Test]
        public void PreemptPendingRequestWhenNewerOneIsSent()
        {
            UniTask<int> first = McpEcsRequest.SendAsync(world, entity, new TestEcsRequest { Payload = 1 }, -1);
            UniTask<int> second = McpEcsRequest.SendAsync(world, entity, new TestEcsRequest { Payload = 2 }, -1);

            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(first.GetAwaiter().GetResult(), Is.EqualTo(-1));
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(world.Get<TestEcsRequest>(entity).Payload, Is.EqualTo(2));
        }

        [Test]
        public void CompleteAndRemoveResolvesAwaiterAfterRemoval()
        {
            UniTask<int> task = McpEcsRequest.SendAsync(world, entity, new TestEcsRequest(), -1);
            TestEcsRequest ecsRequest = world.Get<TestEcsRequest>(entity);

            McpEcsRequest.CompleteAndRemove(world, entity, ecsRequest, 42);

            Assert.That(world.Has<TestEcsRequest>(entity), Is.False);
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(task.GetAwaiter().GetResult(), Is.EqualTo(42));
        }

        [Test]
        public void CompleteAndRemoveTolerateMissingCompletion()
        {
            world.Add(entity, new TestEcsRequest());

            McpEcsRequest.CompleteAndRemove(world, entity, world.Get<TestEcsRequest>(entity), 0);

            Assert.That(world.Has<TestEcsRequest>(entity), Is.False);
        }
    }
}
