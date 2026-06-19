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

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarGhostSystemShould : UnitySystemTestBase<AvatarGhostSystem>
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

        // Creates a minimal AvatarBase with GhostGameObject and GhostRenderer wired up.
        // AvatarBase.Awake() early-returns when AvatarAnimator is null, so GhostRenderer is never
        // assigned through the normal path — we inject both fields directly via reflection.
        internal AvatarBase CreateAvatarBase()
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

        internal static Profile CreateProfile() =>
            new ProfileBuilder()
               .WithUserId("test-user")
               .WithUserNameColor(Color.blue)
               .Build();

        [Test]
        public void AddGhostComponent_AndActivateGhostGO_OnFirstUpdate()
        {
            AvatarBase avatarBase = CreateAvatarBase();
            Entity entity = world.Create(avatarBase, CreateProfile());

            system.Update(0);

            Assert.IsTrue(world.Has<AvatarGhostComponent>(entity));
            ref AvatarGhostComponent ghost = ref world.Get<AvatarGhostComponent>(entity);
            Assert.AreEqual(AvatarGhostPhase.GhostRevealingTransition, ghost.Phase);
            Assert.IsTrue(avatarBase.GhostGameObject.activeSelf);
        }

        [Test]
        public void RemainsInRevealingPhase_BeforeFullDuration()
        {
            AvatarBase avatarBase = CreateAvatarBase();
            Entity entity = world.Create(avatarBase, CreateProfile());
            system.Update(0);

            system.Update(AvatarGhostSystem.REVEAL_DURATION_SEC * 0.5f);

            ref AvatarGhostComponent ghost = ref world.Get<AvatarGhostComponent>(entity);
            Assert.AreEqual(AvatarGhostPhase.GhostRevealingTransition, ghost.Phase);
        }

        [Test]
        public void TransitionsToVisible_AfterFullRevealDuration()
        {
            AvatarBase avatarBase = CreateAvatarBase();
            Entity entity = world.Create(avatarBase, CreateProfile());
            system.Update(0);

            system.Update(AvatarGhostSystem.REVEAL_DURATION_SEC);

            ref AvatarGhostComponent ghost = ref world.Get<AvatarGhostComponent>(entity);
            Assert.AreEqual(AvatarGhostPhase.Visible, ghost.Phase);
        }

        [Test]
        public void DeactivatesGhostGO_WhenEntityDeletedMidReveal()
        {
            AvatarBase avatarBase = CreateAvatarBase();
            Entity entity = world.Create(avatarBase, CreateProfile());
            system.Update(0);
            system.Update(AvatarGhostSystem.REVEAL_DURATION_SEC * 0.5f);

            Assert.AreEqual(AvatarGhostPhase.GhostRevealingTransition, world.Get<AvatarGhostComponent>(entity).Phase);

            world.Add(entity, new DeleteEntityIntention());
            cleanupSystem.Update(0);

            Assert.IsFalse(avatarBase.GhostGameObject.activeSelf);
        }

        [Test]
        public void EachAvatar_OwnsUniqueGhostMaterialInstance_NotTheTemplate()
        {
            AvatarBase avatarBase1 = CreateAvatarBase();
            AvatarBase avatarBase2 = CreateAvatarBase();
            Entity entity1 = world.Create(avatarBase1, CreateProfile());
            Entity entity2 = world.Create(avatarBase2, CreateProfile());

            system.Update(0);

            ref AvatarGhostComponent ghost1 = ref world.Get<AvatarGhostComponent>(entity1);
            ref AvatarGhostComponent ghost2 = ref world.Get<AvatarGhostComponent>(entity2);

            Assert.AreNotSame(ghostMaterialTemplate, ghost1.GhostMaterial, "Entity1 must not share the template material");
            Assert.AreNotSame(ghostMaterialTemplate, ghost2.GhostMaterial, "Entity2 must not share the template material");
            Assert.AreNotSame(ghost1.GhostMaterial, ghost2.GhostMaterial, "Each avatar must own a distinct instance");
        }
    }
}
