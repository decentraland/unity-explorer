using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.InputModifier.Components;
using DCL.SDKComponents.PlayerInputMovement.Systems;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;

namespace DCL.SDKComponents.InputModifier.Tests
{
    /// <summary>
    ///     Integration coverage for the production topology: several per-scene
    ///     <see cref="InputModifierHandlerSystem" /> instances mutating a single shared
    ///     <see cref="InputModifierComponent" /> on the global player entity.
    ///     These are the scenarios the single-system unit tests cannot express.
    /// </summary>
    public class InputModifierHandlerSystemCrossSceneShould
    {
        private static readonly QueryDescription FINALIZE_QUERY = new QueryDescription().WithAll<CRDTEntity>();

        private World globalWorld = null!;
        private Entity playerEntity;
        private ISceneRestrictionBusController busController = null!;

        private World sceneWorldA = null!;
        private ISceneStateProvider sceneStateA = null!;
        private InputModifierHandlerSystem systemA = null!;

        private World sceneWorldB = null!;
        private ISceneStateProvider sceneStateB = null!;
        private InputModifierHandlerSystem systemB = null!;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            playerEntity = globalWorld.Create(new InputModifierComponent());
            busController = Substitute.For<ISceneRestrictionBusController>();

            sceneWorldA = World.Create();
            sceneStateA = Substitute.For<ISceneStateProvider>();
            systemA = new InputModifierHandlerSystem(sceneWorldA, globalWorld, playerEntity, sceneStateA, busController);

            sceneWorldB = World.Create();
            sceneStateB = Substitute.For<ISceneStateProvider>();
            systemB = new InputModifierHandlerSystem(sceneWorldB, globalWorld, playerEntity, sceneStateB, busController);
        }

        [TearDown]
        public void TearDown()
        {
            systemA.Dispose();
            systemB.Dispose();
            sceneWorldA.Dispose();
            sceneWorldB.Dispose();
            globalWorld.Dispose();
        }

        [Test]
        public void ClearGlider_WhenTeleportingFromRestrictingSceneToUnrestrictedScene()
        {
            // Faithful repro of the playtest bug: scene A (Shroom Zoom) disables gliding,
            // player teleports to scene B (Eternal Vortex) which imposes no restriction.
            sceneStateA.IsCurrent.Returns(true);
            AddPlayerModifier(sceneWorldA, new PBInputModifier.Types.StandardInput { DisableGliding = true });
            systemA.Update(0);
            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableGliding);

            // Teleport A -> B
            sceneStateA.IsCurrent.Returns(false);
            systemA.OnSceneIsCurrentChanged(false);

            sceneStateB.IsCurrent.Returns(true);
            systemB.OnSceneIsCurrentChanged(true);
            systemB.Update(0);

            // The glider must be available again in scene B
            Assert.IsFalse(globalWorld.Get<InputModifierComponent>(playerEntity).DisableGliding);
        }

        [Test]
        public void NotClobberActiveSceneRestriction_WhenAnotherSceneUnloadsWithoutApplying()
        {
            // Scene A is current and restricts walking. Scene B is loaded in the background,
            // never applied anything, and now unloads. Its teardown must leave A's restriction intact.
            sceneStateA.IsCurrent.Returns(true);
            AddPlayerModifier(sceneWorldA, new PBInputModifier.Types.StandardInput { DisableWalk = true });
            systemA.Update(0);
            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableWalk);

            // Scene B unloads and transitions to non-current without ever asserting a modifier
            systemB.OnSceneIsCurrentChanged(false);
            systemB.FinalizeComponents(sceneWorldB.Query(in FINALIZE_QUERY));

            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableWalk);
        }

        [Test]
        public void HandOverRestrictionCleanly_WhenTeleportingBetweenRestrictingScenes()
        {
            // Scene A disables gliding, scene B disables jumping. After the teleport the global
            // state must reflect B only: A's gliding lifted, B's jump applied.
            sceneStateA.IsCurrent.Returns(true);
            AddPlayerModifier(sceneWorldA, new PBInputModifier.Types.StandardInput { DisableGliding = true });
            systemA.Update(0);

            // Teleport A -> B
            sceneStateA.IsCurrent.Returns(false);
            systemA.OnSceneIsCurrentChanged(false);

            sceneStateB.IsCurrent.Returns(true);
            AddPlayerModifier(sceneWorldB, new PBInputModifier.Types.StandardInput { DisableJump = true });
            systemB.OnSceneIsCurrentChanged(true);
            systemB.Update(0);

            InputModifierComponent global = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsTrue(global.DisableJump);
            Assert.IsFalse(global.DisableGliding);

            // The movement-blocking indicator reflects B's jump restriction
            busController.Received().PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.Applied));
        }

        private static void AddPlayerModifier(World sceneWorld, PBInputModifier.Types.StandardInput standard)
        {
            Entity entity = sceneWorld.Create();
            sceneWorld.Add(entity, new PBInputModifier { Standard = standard, IsDirty = true }, new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
        }
    }
}
