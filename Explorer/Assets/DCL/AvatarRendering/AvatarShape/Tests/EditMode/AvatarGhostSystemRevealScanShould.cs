using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    // The [None(AvatarGhostFinishedTag)] filters collapse each reveal query's steady-state scan set from every
    // avatar ever spawned to only those still revealing. Per-body visit counters confirm the 50 finished avatars
    // are excluded and only the single still-revealing avatar is scanned (every counter is exactly 1).
    [Category("Performance")]
    public class AvatarGhostSystemRevealScanShould : UnitySystemTestBase<AvatarGhostSystem>
    {
        private const int FINISHED_AVATARS = 50;

                private Material ghostMaterialTemplate = null!;
        private readonly List<GameObject> createdGameObjects = new ();

        [SetUp]
        public void SetUp()
        {
            ghostMaterialTemplate = new Material(Shader.Find("Standard"));
            system = new AvatarGhostSystem(world, ghostMaterialTemplate);
        }

        protected override void OnTearDown()
        {
            if (ghostMaterialTemplate != null)
                Object.DestroyImmediate(ghostMaterialTemplate);

            foreach (GameObject go in createdGameObjects)
                if (go != null)
                    Object.DestroyImmediate(go);

            createdGameObjects.Clear();
        }

        private AvatarBase CreateAvatarBase(string name)
        {
            var root = new GameObject(name);
            createdGameObjects.Add(root);
            return root.AddComponent<AvatarBase>();
        }

        [Test]
        public void ExcludeFinishedAvatarsFromRevealQueries()
        {
            // 50 fully-revealed avatars: terminal Hidden phase + AvatarGhostFinishedTag. These must drop out of the
            // scanned archetype entirely (they early-out of every body anyway, so skipping them is behaviour-neutral).
            for (var i = 0; i < FINISHED_AVATARS; i++)
            {
                var finishedGhost = new AvatarGhostComponent(ghostMaterialTemplate)
                {
                    Phase = AvatarGhostPhase.Hidden,
                    WearablesHidden = true,
                };

                world.Create(
                    new AvatarShapeComponent($"finished-{i}", $"finished-{i}"),
                    finishedGhost,
                    new AvatarGhostFinishedTag(),
                    CreateAvatarBase($"FinishedAvatar-{i}"));
            }

            // One still-revealing avatar (no tag). It legitimately matches all four queries and is the ONLY entity that
            // should be scanned. Phase=Visible + WearablesHidden=false makes every body early-out before dereferencing
            // wearables/AvatarBase, so no scene rig is required.
            var activeGhost = new AvatarGhostComponent(ghostMaterialTemplate)
            {
                Phase = AvatarGhostPhase.Visible,
                WearablesHidden = false,
            };

            world.Create(
                new AvatarShapeComponent("active", "active"),
                activeGhost,
                CreateAvatarBase("ActiveAvatar"));

            AvatarGhostSystem.ResetVisitCounters();

            system!.Update(0.016f);

            Assert.AreEqual(1, AvatarGhostSystem.HideNewlyInstantiatedWearablesVisits,
                "HideNewlyInstantiatedWearables must scan only the revealing avatar, not the 50 finished ones.");
            Assert.AreEqual(1, AvatarGhostSystem.CheckWearablesReadyStartRevealTransitionVisits,
                "CheckWearablesReadyStartRevealTransition must scan only the revealing avatar, not the 50 finished ones.");
            Assert.AreEqual(1, AvatarGhostSystem.UpdateGhostRevealAnimationVisits,
                "UpdateGhostRevealAnimation must scan only the revealing avatar, not the 50 finished ones.");
            Assert.AreEqual(1, AvatarGhostSystem.UpdateRevealTransitionAnimationVisits,
                "UpdateRevealTransitionAnimation must scan only the revealing avatar, not the 50 finished ones.");
        }
    }
}
