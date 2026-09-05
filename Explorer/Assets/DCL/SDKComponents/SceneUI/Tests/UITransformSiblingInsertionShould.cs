using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Tests
{
    /// <summary>
    ///     Integration tests that exercise the cross-system interaction when a new UI sibling
    ///     is inserted between existing siblings. This is the exact scenario that occurs when
    ///     a conditionally-rendered UiEntity appears after its siblings are already in the tree.
    ///
    ///     The test simulates the pipeline:
    ///     1. AddChild (from UITransformParentingSystem) — adds the new entity to the linked list
    ///     2. UITransformSortingSystem — resolves rightOf changes and rebuilds the linked list
    ///
    ///     The critical scenario: when Green is added with rightOf=Red, and Blue's rightOf needs
    ///     to be updated from Red→Green, the sorting system must process Blue's dirty flag and
    ///     update the node's RightOf BEFORE RebuildLinkedList runs.
    /// </summary>
    public class UITransformSiblingInsertionShould : UnitySystemTestBase<UITransformSortingSystem>
    {
        private Dictionary<CRDTEntity, Entity> entitiesMap;
        private Entity sceneRoot;
        private UITransformComponent rootComponent;

        [SetUp]
        public void SetUp()
        {
            entitiesMap = new Dictionary<CRDTEntity, Entity>();
            system = new UITransformSortingSystem(world, entitiesMap);
            CreateRoot();
        }

        private void CreateRoot()
        {
            rootComponent = new UITransformComponent();
            rootComponent.InitializeAsRoot(new VisualElement());
            entitiesMap[SpecialEntitiesID.SCENE_ROOT_ENTITY] = sceneRoot = world.Create(
                new CRDTEntity(SpecialEntitiesID.SCENE_ROOT_ENTITY),
                rootComponent
            );
        }

        /// <summary>
        ///     Creates a child entity with UITransformComponent and PBUiTransform, adds it to the
        ///     parent's linked list via AddChild (simulating UITransformParentingSystem), and adds
        ///     its VisualElement to the parent's ContentContainer.
        /// </summary>
        private Entity CreateChild(int crdtId, int rightOf)
        {
            var crdtEntity = new CRDTEntity(crdtId);
            var sdkModel = new PBUiTransform { IsDirty = true, RightOf = rightOf };
            var ve = new VisualElement();

            var component = new UITransformComponent
            {
                RelationData = new UITransformRelationLinkedData { rightOf = rightOf },
                Transform = ve,
            };

            rootComponent.ContentContainer.Add(ve);

            Entity e = world.Create(crdtEntity, sdkModel, component);
            entitiesMap[crdtEntity] = e;
            rootComponent.RelationData.AddChild(sceneRoot, crdtEntity, ref component.RelationData);
            return e;
        }

        private VisualElement GetVisualElement(Entity entity) =>
            world.Get<UITransformComponent>(entity).Transform;

        [Test]
        [Description("Reproduces the exact bug scenario: 2 siblings exist (A, B), then a new sibling C is " +
                      "inserted between them. The SDK sends C with rightOf=A and updates B's rightOf to C. " +
                      "After all systems run, the order must be A → C → B.")]
        public void InsertNewChildBetweenExistingSiblings()
        {
            // Arrange — Create initial children: A → B
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);

            // Run sorting to establish initial order
            system.Update(0);

            // Verify initial order
            CollectionAssert.AreEqual(
                new[] { GetVisualElement(entityA), GetVisualElement(entityB) },
                rootComponent.ContentContainer.Children().ToArray(),
                "Initial order should be A → B"
            );

            // Reset dirty flags to simulate a new frame
            world.Get<PBUiTransform>(entityA).IsDirty = false;
            world.Get<PBUiTransform>(entityB).IsDirty = false;

            // Act — Insert C between A and B
            // Simulates what UITransformParentingSystem.AddChild does for the new entity:
            Entity entityC = CreateChild(300, 100); // rightOf = A (same as B's current rightOf)

            // Simulates what the SDK sends: B's rightOf updated from A(100) to C(300)
            ref PBUiTransform bModel = ref world.Get<PBUiTransform>(entityB);
            bModel.RightOf = 300;
            bModel.IsDirty = true;

            // Run sorting system (ResolveSiblingsOrder + ApplySorting)
            system.Update(0);

            // Assert — The visual order should be A → C → B
            VisualElement[] expected =
            {
                GetVisualElement(entityA),
                GetVisualElement(entityC),
                GetVisualElement(entityB),
            };

            CollectionAssert.AreEqual(expected, rootComponent.ContentContainer.Children().ToArray(),
                "Order should be A → C → B after inserting C between A and B");
        }

        [Test]
        [Description("Tests the failure case: C is inserted with rightOf=A, but B's rightOf update is " +
                      "NOT received (IsDirty=false). Both B and C have rightOf=A (duplicate). " +
                      "RebuildLinkedList must not silently drop any node.")]
        public void InsertNewChildBetweenExistingSiblings_WithMissingRightOfUpdate()
        {
            // Arrange — Initial order: A → B
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);

            system.Update(0);

            // Reset dirty flags
            world.Get<PBUiTransform>(entityA).IsDirty = false;
            world.Get<PBUiTransform>(entityB).IsDirty = false;

            // Act — Insert C with rightOf=A, but DO NOT update B's rightOf
            // This simulates the case where B's CRDT update is lost or not processed
            Entity entityC = CreateChild(300, 100);

            // B is NOT dirty, so ResolveSiblingsOrder won't update B's node.RightOf
            // Both C and B now claim to be "after A" → duplicate rightOf in RebuildLinkedList

            system.Update(0);

            // Assert — All 3 children must still be present (none silently dropped)
            VisualElement[] children = rootComponent.ContentContainer.Children().ToArray();

            Assert.That(children.Length, Is.EqualTo(3),
                "All 3 children should be present — none should be dropped due to duplicate rightOf");

            Assert.That(children.Contains(GetVisualElement(entityA)), Is.True, "A should be present");
            Assert.That(children.Contains(GetVisualElement(entityB)), Is.True, "B should be present");
            Assert.That(children.Contains(GetVisualElement(entityC)), Is.True, "C should be present");
        }

        [Test]
        [Description("Tests inserting a new child at the beginning (new head) of existing siblings.")]
        public void InsertNewChildAtBeginning()
        {
            // Arrange — Initial order: A → B
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);

            system.Update(0);

            // Reset dirty flags
            world.Get<PBUiTransform>(entityA).IsDirty = false;
            world.Get<PBUiTransform>(entityB).IsDirty = false;

            // Act — Insert C as new head (rightOf=0), update A to rightOf=C(300)
            Entity entityC = CreateChild(300, 0);

            ref PBUiTransform aModel = ref world.Get<PBUiTransform>(entityA);
            aModel.RightOf = 300;
            aModel.IsDirty = true;

            system.Update(0);

            // Assert — Order should be C → A → B
            VisualElement[] expected =
            {
                GetVisualElement(entityC),
                GetVisualElement(entityA),
                GetVisualElement(entityB),
            };

            CollectionAssert.AreEqual(expected, rootComponent.ContentContainer.Children().ToArray(),
                "Order should be C → A → B after inserting C as new head");
        }

        [Test]
        [Description("Tests inserting a new child at the end of existing siblings.")]
        public void InsertNewChildAtEnd()
        {
            // Arrange — Initial order: A → B
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);

            system.Update(0);

            // Reset dirty flags
            world.Get<PBUiTransform>(entityA).IsDirty = false;
            world.Get<PBUiTransform>(entityB).IsDirty = false;

            // Act — Insert C at the end (rightOf=B(200))
            Entity entityC = CreateChild(300, 200);

            system.Update(0);

            // Assert — Order should be A → B → C
            VisualElement[] expected =
            {
                GetVisualElement(entityA),
                GetVisualElement(entityB),
                GetVisualElement(entityC),
            };

            CollectionAssert.AreEqual(expected, rootComponent.ContentContainer.Children().ToArray(),
                "Order should be A → B → C after inserting C at the end");
        }

        [Test]
        [Description("Tests inserting two new children simultaneously between existing siblings.")]
        public void InsertTwoNewChildrenBetweenExistingSiblings()
        {
            // Arrange — Initial order: A → B
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);

            system.Update(0);

            // Reset dirty flags
            world.Get<PBUiTransform>(entityA).IsDirty = false;
            world.Get<PBUiTransform>(entityB).IsDirty = false;

            // Act — Insert C and D between A and B: A → C → D → B
            // C: rightOf=A(100)
            // D: rightOf=C(300)
            // B: rightOf updated to D(400)
            Entity entityC = CreateChild(300, 100);
            Entity entityD = CreateChild(400, 300);

            ref PBUiTransform bModel = ref world.Get<PBUiTransform>(entityB);
            bModel.RightOf = 400;
            bModel.IsDirty = true;

            system.Update(0);

            // Assert — Order should be A → C → D → B
            VisualElement[] expected =
            {
                GetVisualElement(entityA),
                GetVisualElement(entityC),
                GetVisualElement(entityD),
                GetVisualElement(entityB),
            };

            CollectionAssert.AreEqual(expected, rootComponent.ContentContainer.Children().ToArray(),
                "Order should be A → C → D → B");
        }

        [Test]
        [Description("Tests that RebuildLinkedList handles multiple duplicate rightOf targets gracefully.")]
        public void HandleMultipleDuplicateRightOfTargets()
        {
            // Arrange — Initial order: A → B
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);

            system.Update(0);

            // Reset dirty flags
            world.Get<PBUiTransform>(entityA).IsDirty = false;
            world.Get<PBUiTransform>(entityB).IsDirty = false;

            // Act — Insert C and D, both with rightOf=A(100), creating a triple collision
            // B already has rightOf=A(100), and neither B nor the new nodes are updated
            Entity entityC = CreateChild(300, 100);
            Entity entityD = CreateChild(400, 100);

            system.Update(0);

            // Assert — All 4 children must be present
            VisualElement[] children = rootComponent.ContentContainer.Children().ToArray();

            Assert.That(children.Length, Is.EqualTo(4),
                "All 4 children should be present — none dropped from triple rightOf collision");

            Assert.That(children.Contains(GetVisualElement(entityA)), Is.True, "A should be present");
            Assert.That(children.Contains(GetVisualElement(entityB)), Is.True, "B should be present");
            Assert.That(children.Contains(GetVisualElement(entityC)), Is.True, "C should be present");
            Assert.That(children.Contains(GetVisualElement(entityD)), Is.True, "D should be present");
        }

        [Test]
        [Description("Reproduces the root cause: existing children have ZIndex=0 (from UITransformUpdateSystem) " +
                      "while a newly inserted child has ZIndex=null. With the old code (tabIndex = ZIndex ?? i), " +
                      "existing children all got tabIndex=0 and the new child got its positional index, breaking order.")]
        public void InsertNewChildBetweenExistingSiblings_WithZIndexZeroOnExistingChildren()
        {
            // Arrange — Create initial children: A → B
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);

            // Run sorting to establish initial order
            system.Update(0);

            // Simulate what UITransformUpdateSystem does on the NEXT frame:
            // it sets ZIndex = 0 for all entities because the SDK sends HasZIndex=true, ZIndex=0
            world.Get<UITransformComponent>(entityA).ZIndex = 0;
            world.Get<UITransformComponent>(entityB).ZIndex = 0;

            // Reset dirty flags to simulate a new frame
            world.Get<PBUiTransform>(entityA).IsDirty = false;
            world.Get<PBUiTransform>(entityB).IsDirty = false;

            // Act — Insert C between A and B (C has ZIndex=null since it's brand new)
            Entity entityC = CreateChild(300, 100);
            Assert.That(world.Get<UITransformComponent>(entityC).ZIndex, Is.Null,
                "Newly created entity should have ZIndex=null");

            // Update B's rightOf
            ref PBUiTransform bModel = ref world.Get<PBUiTransform>(entityB);
            bModel.RightOf = 300;
            bModel.IsDirty = true;

            system.Update(0);

            // Assert — Order must be A → C → B (not A → B → C)
            VisualElement[] expected =
            {
                GetVisualElement(entityA),
                GetVisualElement(entityC),
                GetVisualElement(entityB),
            };

            CollectionAssert.AreEqual(expected, rootComponent.ContentContainer.Children().ToArray(),
                "Order should be A → C → B even when A and B have ZIndex=0 and C has ZIndex=null");
        }
    }
}
