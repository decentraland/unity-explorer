using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.AvatarRendering.AvatarShape.PlayModeTests
{
    public class AvatarGhostCleanupShould : UnitySystemTestBase<AvatarGhostSystem>
    {
        private AvatarGhostCleanupSystem cleanupSystem;
        private Material ghostMaterialTemplate;
        private readonly List<GameObject> createdGameObjects = new ();

        [SetUp]
        public void SetUp()
        {
            ghostMaterialTemplate = new Material(Shader.Find("Standard"));
            system = new AvatarGhostSystem(world, ghostMaterialTemplate);
            cleanupSystem = new AvatarGhostCleanupSystem(world);
        }

        protected override void OnTearDown()
        {
            cleanupSystem?.Dispose();
            Object.DestroyImmediate(ghostMaterialTemplate);

            foreach (GameObject go in createdGameObjects)
                if (go != null)
                    Object.DestroyImmediate(go);

            createdGameObjects.Clear();
        }

        private AvatarBase CreateAvatarBase()
        {
            var root = new GameObject("AvatarRoot");
            createdGameObjects.Add(root);

            var ghostGO = new GameObject("Ghost");
            ghostGO.transform.SetParent(root.transform);
            ghostGO.SetActive(false);
            Renderer renderer = ghostGO.AddComponent<MeshRenderer>();

            AvatarBase avatarBase = root.AddComponent<AvatarBase>();

            typeof(AvatarBase)
               .GetField("<GhostGameObject>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
               .SetValue(avatarBase, ghostGO);

            typeof(AvatarBase)
               .GetField("<GhostRenderer>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
               .SetValue(avatarBase, renderer);

            return avatarBase;
        }

        private static Profile CreateProfile() =>
            new ProfileBuilder()
               .WithUserId("test-user")
               .WithUserNameColor(Color.blue)
               .Build();

        [UnityTest]
        public IEnumerator DestroysGhostMaterial_OnEntityCleanup()
        {
            AvatarBase avatarBase = CreateAvatarBase();
            Entity entity = world.Create(avatarBase, CreateProfile());
            system.Update(0);

            Material createdMaterial = world.Get<AvatarGhostComponent>(entity).GhostMaterial;
            Assert.IsTrue(createdMaterial != null, "Material must be alive before cleanup");

            world.Add(entity, new DeleteEntityIntention());
            cleanupSystem.Update(0);

            yield return null; // flush deferred Object.Destroy

            Assert.IsTrue(createdMaterial == null, "Ghost material must be destroyed on cleanup — leaking it wastes GPU memory");
        }

        [UnityTest]
        public IEnumerator DestroysAllGhostMaterials_OnWorldDispose()
        {
            AvatarBase avatarBase1 = CreateAvatarBase();
            AvatarBase avatarBase2 = CreateAvatarBase();
            world.Create(avatarBase1, CreateProfile());
            world.Create(avatarBase2, CreateProfile());
            system.Update(0);

            var materials = new List<Material>();

            world.Query(
                new QueryDescription().WithAll<AvatarGhostComponent>(),
                (ref AvatarGhostComponent g) => materials.Add(g.GhostMaterial));

            Assert.AreEqual(2, materials.Count);

            cleanupSystem.Dispose();

            yield return null; // flush deferred Object.Destroy

            foreach (Material mat in materials)
                Assert.IsTrue(mat == null, "All ghost materials must be destroyed on world dispose");
        }
    }
}
