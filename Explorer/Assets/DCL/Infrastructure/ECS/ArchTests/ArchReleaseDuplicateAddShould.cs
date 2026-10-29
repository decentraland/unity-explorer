using Arch.Core;
using NUnit.Framework;
using System.Collections.Generic;

#if !DEBUG_ARCH

namespace ECS.ArchTests
{
    /// <summary>
    ///     Documents how the Release Arch build corrupts an entity when a component it already has is added again (UNITY-EXPLORER-Q2N),
    ///     which is why every <c>World.Add</c> on a possibly present component is guarded with <c>Has</c>. A failure here means Arch was fixed.
    /// </summary>
    public class ArchReleaseDuplicateAddShould
    {
        private struct Tag { }

        private struct Payload
        {
            public int Value;
        }

        private struct Moved { }

        private World world = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void AliasNextEntityCreatedInSameArchetype()
        {
            // Arrange
            Entity first = world.Create(new Tag(), new Payload { Value = 1 });

            // Act
            world.Add(first, new Tag());
            Entity second = world.Create(new Tag(), new Payload { Value = 2 });

            // Assert
            Assert.That(world.Get<Payload>(first).Value, Is.EqualTo(2), "first should read the slot handed to second");
            Assert.That(world.Get<Payload>(second).Value, Is.EqualTo(2));
        }

        [Test]
        public void SwapNeighbourPastArchetypeCountWhenMovingOut()
        {
            // Arrange
            Entity first = world.Create(new Tag(), new Payload { Value = 1 });
            Entity neighbour = world.Create(new Tag(), new Payload { Value = 2 });

            // Act
            world.Add(first, new Tag());
            world.Add(first, new Moved());
            Entity third = world.Create(new Tag(), new Payload { Value = 3 });
            Entity fourth = world.Create(new Tag(), new Payload { Value = 4 });

            // Assert
            Assert.That(world.Get<Payload>(neighbour).Value, Is.EqualTo(4), "neighbour should read the slot handed to fourth");
            Assert.That(world.Get<Payload>(third).Value, Is.EqualTo(3));
            Assert.That(world.Get<Payload>(fourth).Value, Is.EqualTo(4));
        }

        [Test]
        public void IterateGhostCopyInsteadOfNeighbourWhenMovingOut()
        {
            // Arrange
            Entity first = world.Create(new Tag(), new Payload { Value = 1 });
            Entity neighbour = world.Create(new Tag(), new Payload { Value = 2 });
            world.Add(first, new Tag());
            world.Add(first, new Moved());

            var seen = new List<Entity>();
            var query = new QueryDescription().WithAll<Tag, Payload>();

            // Act
            world.Query(in query, entity => seen.Add(entity));

            // Assert
            Assert.That(seen, Is.EqualTo(new[] { first, first }), "first should be iterated in both archetypes and neighbour not at all");
            Assert.That(seen, Has.No.Member(neighbour));
        }
    }
}

#endif
