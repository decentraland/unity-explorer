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

        private void CreateEntities(int seed)
        {
            var root = new UITransformComponent { Transform = new VisualElement() };
            entitiesMap[SpecialEntitiesID.SCENE_ROOT_ENTITY] = sceneRoot = world.Create(root);

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
                root.Transform.Add(vs);

                var component = new UITransformComponent
                {
                    RelationData = new UITransformRelationLinkedData { rightOf = rightOf },
                    Transform = vs,
                };

                Entity e = world.Create(crdtEntity, sdkModel, component);

                entitiesMap[crdtEntity] = e;
                root.RelationData.AddChild(world.Reference(sceneRoot), crdtEntity, ref component.RelationData);
            }
        }
    }
}
