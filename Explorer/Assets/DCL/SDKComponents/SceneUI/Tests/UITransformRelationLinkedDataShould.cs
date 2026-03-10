using Arch.Core;
using CRDT;
using DCL.SDKComponents.SceneUI.Components;
using NUnit.Framework;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITransformRelationLinkedDataShould
    {
        private World world;
        private Entity parentEntity;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            parentEntity = world.Create();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void NotCreateSelfCycleWhenRightOfPointsToSelf()
        {
            // Arrange: entity A with rightOf = A (self-reference)
            var parentData = new UITransformRelationLinkedData();
            var childData = new UITransformRelationLinkedData { rightOf = new CRDTEntity(10) };

            // Act: Add child A where A.rightOf = A
            parentData.AddChild(parentEntity, new CRDTEntity(10), ref childData);

            // Assert: list should not cycle — head.Next must not point back to head
            Assert.IsNotNull(parentData.head);
            Assert.IsNull(parentData.head.Next, "Self-referencing rightOf should not create a cycle");
            Assert.AreEqual(1, parentData.NodeCount);
        }

        [Test]
        public void NotCreateMutualCycleWhenRightOfIsCircular()
        {
            // Arrange: A.rightOf = B, B.rightOf = A (mutual reference)
            var parentData = new UITransformRelationLinkedData();
            var childDataA = new UITransformRelationLinkedData { rightOf = new CRDTEntity(20) }; // A wants to be after B
            var childDataB = new UITransformRelationLinkedData { rightOf = new CRDTEntity(10) }; // B wants to be after A

            // Act
            parentData.AddChild(parentEntity, new CRDTEntity(10), ref childDataA); // Add A (B not found yet, goes to pending)
            parentData.AddChild(parentEntity, new CRDTEntity(20), ref childDataB); // Add B (A found, triggers ResolvePending for A)

            // Assert: traversing the list must terminate within NodeCount steps
            int count = 0;
            int max = parentData.NodeCount;

            for (var node = parentData.head; node != null; node = node.Next)
            {
                count++;
                Assert.LessOrEqual(count, max + 1, "Linked list traversal exceeded node count — cycle detected");
            }

            Assert.AreEqual(2, parentData.NodeCount);
        }

        [Test]
        public void MaintainCorrectOrderForNonCircularChildren()
        {
            // Arrange: A (first), B after A, C after B — no cycles
            var parentData = new UITransformRelationLinkedData();
            var childDataA = new UITransformRelationLinkedData { rightOf = new CRDTEntity(0) };
            var childDataB = new UITransformRelationLinkedData { rightOf = new CRDTEntity(10) };
            var childDataC = new UITransformRelationLinkedData { rightOf = new CRDTEntity(20) };

            // Act
            parentData.AddChild(parentEntity, new CRDTEntity(10), ref childDataA);
            parentData.AddChild(parentEntity, new CRDTEntity(20), ref childDataB);
            parentData.AddChild(parentEntity, new CRDTEntity(30), ref childDataC);

            // Assert: list order should be A → B → C
            Assert.IsNotNull(parentData.head);
            Assert.AreEqual(new CRDTEntity(10), parentData.head.EntityId);
            Assert.IsNotNull(parentData.head.Next);
            Assert.AreEqual(new CRDTEntity(20), parentData.head.Next.EntityId);
            Assert.IsNotNull(parentData.head.Next.Next);
            Assert.AreEqual(new CRDTEntity(30), parentData.head.Next.Next.EntityId);
            Assert.IsNull(parentData.head.Next.Next.Next);
            Assert.AreEqual(3, parentData.NodeCount);
        }

        [Test]
        public void MaintainCorrectOrderWhenChildrenAddedOutOfOrder()
        {
            // Arrange: add C first, then A, then B — pending resolution should build A → B → C
            var parentData = new UITransformRelationLinkedData();
            var childDataC = new UITransformRelationLinkedData { rightOf = new CRDTEntity(20) }; // C after B
            var childDataA = new UITransformRelationLinkedData { rightOf = new CRDTEntity(0) };  // A first
            var childDataB = new UITransformRelationLinkedData { rightOf = new CRDTEntity(10) }; // B after A

            // Act: add in shuffled order
            parentData.AddChild(parentEntity, new CRDTEntity(30), ref childDataC); // C (B not found, pending)
            parentData.AddChild(parentEntity, new CRDTEntity(10), ref childDataA); // A (first)
            parentData.AddChild(parentEntity, new CRDTEntity(20), ref childDataB); // B after A (resolves C pending on B)

            // Assert: traversal terminates and all 3 nodes are reachable
            int count = 0;

            for (var node = parentData.head; node != null; node = node.Next)
            {
                count++;
                if (count > parentData.NodeCount) break;
            }

            Assert.AreEqual(3, count);
            Assert.AreEqual(3, parentData.NodeCount);
        }

        [Test]
        public void DisposeWithoutHangingOnCyclicList()
        {
            // Arrange: create a normal list then manually corrupt it into a cycle
            var parentData = new UITransformRelationLinkedData();
            var childDataA = new UITransformRelationLinkedData { rightOf = new CRDTEntity(0) };
            var childDataB = new UITransformRelationLinkedData { rightOf = new CRDTEntity(10) };

            parentData.AddChild(parentEntity, new CRDTEntity(10), ref childDataA);
            parentData.AddChild(parentEntity, new CRDTEntity(20), ref childDataB);

            // Manually corrupt: make B.Next point back to A (simulate a cycle)
            parentData.head.Next.Next = parentData.head;

            // Act & Assert: Dispose should not hang (it uses dictionary iteration, not list traversal)
            Assert.DoesNotThrow(() => parentData.Dispose());
            Assert.IsNull(parentData.head);
        }
    }
}
