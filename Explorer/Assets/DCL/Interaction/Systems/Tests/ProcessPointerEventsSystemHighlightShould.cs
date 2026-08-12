using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Utility;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using InputAction = DCL.ECSComponents.InputAction;
using UnityInputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.Interaction.Systems.Tests
{
    public class ProcessPointerEventsSystemHighlightShould
    {
                private World world = null!;
                private ProcessPointerEventsSystem system = null!;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            system = new ProcessPointerEventsSystem(
                world,
                new Dictionary<InputAction, UnityInputAction>(),
                Substitute.For<IEntityCollidersGlobalCache>(),
                Substitute.For<IEventSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        // HighlightNewEntity must scan the scene world's archetypes (CountEntities(highlightQuery)) only ONCE per World
        // — the first hover — then serve the "does the tracker entity exist?" answer from a cached per-World flag.
        // HighlightScanCount is bumped exactly at that scan site. Also asserts no duplicate tracker entity is created.
        [Test]
        public void ScanForHighlightEntityOncePerWorld()
        {
            GlobalColliderSceneEntityInfo info = new (
                new SceneEcsExecutor(world),
                new ColliderSceneEntityInfo(world.Create(new CRDTEntity(1)), 1, ColliderLayer.ClPhysics));

            const int FRAMES = 600;

            for (var i = 0; i < FRAMES; i++)
                system.HighlightNewEntity(info, true);

            Assert.AreEqual(1, system.HighlightScanCount,
                "The highlight-existence archetype scan must run once per World, not once per frame.");

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<HighlightComponent>()),
                "Exactly one highlight tracker entity must exist — the cache must not double-create.");
        }
    }
}
