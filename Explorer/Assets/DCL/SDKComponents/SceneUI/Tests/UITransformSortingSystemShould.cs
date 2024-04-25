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
    public class UITransformSortingSystemShould : UnitySystemTestBase<UITransformSortingSystem>
    {
        private static readonly int[] ORDER = Enumerable.Range(0, 10).ToArray();

        private Dictionary<CRDTEntity, Entity> entitiesMap;
        private Entity sceneRoot;

        [SetUp]
        public void SetUp()
        {
            CreateEntities();

            system = new UITransformSortingSystem(world, entitiesMap, sceneRoot);
        }

        [Test]
        [Repeat(5)]
        public void SortChildren()
        {
            // Act
            system.Update(0);

            // Assert
            var expected = ORDER.Select(i => world.Get<UITransformComponent>(entitiesMap[i]).Transform).ToArray();
            var actual = world.Get<UITransformComponent>(sceneRoot).Transform.Children();

            CollectionAssert.AreEqual(expected, actual);
        }

        private void CreateEntities()
        {
            entitiesMap = new Dictionary<CRDTEntity, Entity>();

            var root = new UITransformComponent { Transform = new VisualElement(), Children = new HashSet<EntityReference>() };
            entitiesMap[SpecialEntitiesID.SCENE_ROOT_ENTITY] = sceneRoot = world.Create(root);

            // shuffle randomly
            System.Random rng = new System.Random();

            var copy = ORDER.ToArray();
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

                var sdkModel = new PBUiTransform { IsDirty = true };

                var vs = new VisualElement();
                root.Transform.Add(vs);

                var component = new UITransformComponent
                {
                    RightOf = i > 0 ? i - 1 : 0,
                    Transform = vs,
                    Children = new HashSet<EntityReference>()
                };

                var e = world.Create(crdtEntity, sdkModel, component);

                entitiesMap[crdtEntity] = e;
                root.Children.Add(world.Reference(e));
            }
        }
    }
}
