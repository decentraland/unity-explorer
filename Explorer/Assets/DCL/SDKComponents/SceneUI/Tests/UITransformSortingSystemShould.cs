using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITransformSortingSystemShould : UnitySystemTestBase<UITransformSortingSystem>
    {
        private static readonly int[] ORDER = Enumerable.Range(10, 10).ToArray();

        private Dictionary<CRDTEntity, Entity> entitiesMap;
        private Entity sceneRoot;
        private UITransformComponent rootComponent;

        [SetUp]
        public void SetUp()
        {
            entitiesMap = new Dictionary<CRDTEntity, Entity>();
            system = new UITransformSortingSystem(world, entitiesMap);
        }

        [Test]
        public void SortChildren([Values(429673, 23458458, 12, 445, 33068)] int seed)
        {
            CreateEntities(seed);

            // Act
            system.Update(0);

            // Assert
            VisualElement[] expected = ORDER.Select(i => world.Get<UITransformComponent>(entitiesMap[i]).Transform).ToArray();
            IEnumerable<VisualElement>? actual = world.Get<UITransformComponent>(sceneRoot).Transform.Children();

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        [Timeout(5000)]
        public void ReorderThreeChildrenWithoutCreatingCycle()
        {
            // Initial order: A(100) → B(200) → C(300)
            CreateRoot();
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);
            Entity entityC = CreateChild(300, 200);

            system.Update(0);

            // Reorder to: B → A → C
            world.Get<PBUiTransform>(entityB).RightOf = 0;
            world.Get<PBUiTransform>(entityA).RightOf = 200;
            world.Get<PBUiTransform>(entityC).RightOf = 100;

            system.Update(0);

            VisualElement[] expected =
            {
                world.Get<UITransformComponent>(entityB).Transform,
                world.Get<UITransformComponent>(entityA).Transform,
                world.Get<UITransformComponent>(entityC).Transform,
            };

            CollectionAssert.AreEqual(expected, rootComponent.Transform.Children());
        }

        [Test]
        [Timeout(5000)]
        public void MoveChildToHead()
        {
            // Initial order: A(100) → B(200) → C(300)
            CreateRoot();
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);
            Entity entityC = CreateChild(300, 200);

            system.Update(0);

            // Reorder to: C → A → B
            world.Get<PBUiTransform>(entityC).RightOf = 0;
            world.Get<PBUiTransform>(entityA).RightOf = 300;
            world.Get<PBUiTransform>(entityB).RightOf = 100;

            system.Update(0);

            VisualElement[] expected =
            {
                world.Get<UITransformComponent>(entityC).Transform,
                world.Get<UITransformComponent>(entityA).Transform,
                world.Get<UITransformComponent>(entityB).Transform,
            };

            CollectionAssert.AreEqual(expected, rootComponent.Transform.Children());
        }

        [Test]
        [Timeout(5000)]
        public void ReverseChildOrder()
        {
            // Initial order: A(100) → B(200) → C(300)
            CreateRoot();
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);
            Entity entityC = CreateChild(300, 200);

            system.Update(0);

            // Reverse to: C → B → A
            world.Get<PBUiTransform>(entityC).RightOf = 0;
            world.Get<PBUiTransform>(entityB).RightOf = 300;
            world.Get<PBUiTransform>(entityA).RightOf = 200;

            system.Update(0);

            VisualElement[] expected =
            {
                world.Get<UITransformComponent>(entityC).Transform,
                world.Get<UITransformComponent>(entityB).Transform,
                world.Get<UITransformComponent>(entityA).Transform,
            };

            CollectionAssert.AreEqual(expected, rootComponent.Transform.Children());
        }

        [Test]
        [Timeout(5000)]
        public void ReorderFourChildren()
        {
            // Initial order: A(100) → B(200) → C(300) → D(400)
            CreateRoot();
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);
            Entity entityC = CreateChild(300, 200);
            Entity entityD = CreateChild(400, 300);

            system.Update(0);

            // Reorder to: C → A → D → B
            world.Get<PBUiTransform>(entityC).RightOf = 0;
            world.Get<PBUiTransform>(entityA).RightOf = 300;
            world.Get<PBUiTransform>(entityD).RightOf = 100;
            world.Get<PBUiTransform>(entityB).RightOf = 400;

            system.Update(0);

            VisualElement[] expected =
            {
                world.Get<UITransformComponent>(entityC).Transform,
                world.Get<UITransformComponent>(entityA).Transform,
                world.Get<UITransformComponent>(entityD).Transform,
                world.Get<UITransformComponent>(entityB).Transform,
            };

            CollectionAssert.AreEqual(expected, rootComponent.Transform.Children());
        }

        [Test]
        [Timeout(5000)]
        public void HandleConsecutiveReorders()
        {
            // Initial order: A(100) → B(200) → C(300)
            CreateRoot();
            Entity entityA = CreateChild(100, 0);
            Entity entityB = CreateChild(200, 100);
            Entity entityC = CreateChild(300, 200);

            system.Update(0);

            // Reorder 1: A → B → C  →  B → A → C
            world.Get<PBUiTransform>(entityB).RightOf = 0;
            world.Get<PBUiTransform>(entityA).RightOf = 200;
            world.Get<PBUiTransform>(entityC).RightOf = 100;

            system.Update(0);

            VisualElement[] expected1 =
            {
                world.Get<UITransformComponent>(entityB).Transform,
                world.Get<UITransformComponent>(entityA).Transform,
                world.Get<UITransformComponent>(entityC).Transform,
            };

            CollectionAssert.AreEqual(expected1, rootComponent.Transform.Children());

            // Reorder 2: B → A → C  →  C → B → A
            world.Get<PBUiTransform>(entityC).RightOf = 0;
            world.Get<PBUiTransform>(entityB).RightOf = 300;
            world.Get<PBUiTransform>(entityA).RightOf = 200;

            system.Update(0);

            VisualElement[] expected2 =
            {
                world.Get<UITransformComponent>(entityC).Transform,
                world.Get<UITransformComponent>(entityB).Transform,
                world.Get<UITransformComponent>(entityA).Transform,
            };

            CollectionAssert.AreEqual(expected2, rootComponent.Transform.Children());

            // Reorder 3: back to original  C → B → A  →  A → B → C
            world.Get<PBUiTransform>(entityA).RightOf = 0;
            world.Get<PBUiTransform>(entityB).RightOf = 100;
            world.Get<PBUiTransform>(entityC).RightOf = 200;

            system.Update(0);

            VisualElement[] expected3 =
            {
                world.Get<UITransformComponent>(entityA).Transform,
                world.Get<UITransformComponent>(entityB).Transform,
                world.Get<UITransformComponent>(entityC).Transform,
            };

            CollectionAssert.AreEqual(expected3, rootComponent.Transform.Children());
        }

        private void CreateRoot()
        {
            rootComponent = new UITransformComponent { Transform = new VisualElement() };
            entitiesMap[SpecialEntitiesID.SCENE_ROOT_ENTITY] = sceneRoot = world.Create(rootComponent);
        }

        private Entity CreateChild(int id, int rightOf)
        {
            var crdtEntity = new CRDTEntity(id);
            var sdkModel = new PBUiTransform { IsDirty = true, RightOf = rightOf };
            var vs = new VisualElement();
            rootComponent.Transform.Add(vs);

            var component = new UITransformComponent
            {
                RelationData = new UITransformRelationLinkedData { rightOf = rightOf },
                Transform = vs,
            };

            Entity e = world.Create(crdtEntity, sdkModel, component);
            entitiesMap[crdtEntity] = e;
            rootComponent.RelationData.AddChild(sceneRoot, crdtEntity, ref component.RelationData);
            return e;
        }

        private void CreateEntities(int seed)
        {
            CreateRoot();

            // shuffle randomly
            var rng = new Random(seed);

            int firstElement = ORDER.Min();
            int[] copy = ORDER.ToArray();
            int n = ORDER.Length;

            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (copy[k], copy[n]) = (copy[n], copy[k]);
            }

            foreach (int i in copy)
            {
                var crdtEntity = new CRDTEntity(i);

                int rightOf = i == firstElement ? 0 : i - 1;

                var sdkModel = new PBUiTransform { IsDirty = true, RightOf = rightOf };

                var vs = new VisualElement();
                rootComponent.Transform.Add(vs);

                var component = new UITransformComponent
                {
                    RelationData = new UITransformRelationLinkedData { rightOf = rightOf },
                    Transform = vs,
                };

                Entity e = world.Create(crdtEntity, sdkModel, component);

                entitiesMap[crdtEntity] = e;
                rootComponent.RelationData.AddChild(sceneRoot, crdtEntity, ref component.RelationData);
            }
        }
    }
}
