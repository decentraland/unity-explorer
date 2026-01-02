using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.InputModifier.Components;
using DCL.SDKComponents.PlayerInputMovement.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;

namespace DCL.SDKComponents.InputModifier.Tests
{
    public class InputModifierHandlerSystemShould : UnitySystemTestBase<InputModifierHandlerSystem>
    {
        private World globalWorld;
        private Entity playerEntity;
        private ISceneStateProvider sceneStateProvider;
        private ISceneRestrictionBusController sceneRestrictionBusController;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            playerEntity = globalWorld.Create(new InputModifierComponent());

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            sceneRestrictionBusController = Substitute.For<ISceneRestrictionBusController>();

            system = new InputModifierHandlerSystem(world, globalWorld, playerEntity, sceneStateProvider, sceneRestrictionBusController);
        }

        [TearDown]
        protected override void OnTearDown()
        {
            globalWorld.Dispose();
        }

        [Test]
        public void ApplyModifiers_ShouldUpdateGlobalWorld_WhenSceneIsCurrent_AndEntityIsPlayer()
        {
            // Arrange
            var entity = world.Create();
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput
                {
                    DisableWalk = true,
                    DisableRun = true
                },
                IsDirty = true
            };

            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);

            world.Add(entity, pbInputModifier, crdtEntity);

            // Act
            system.Update(0);

            // Assert
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsTrue(inputModifier.DisableWalk);
            Assert.IsTrue(inputModifier.DisableRun);
            Assert.IsFalse(inputModifier.DisableJump);

            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.APPLIED));

            // Check component was added to track removal
            Assert.IsTrue(world.Has<InputModifierComponent>(entity));
        }

        [Test]
        public void ResetModifiers_ShouldBeCalled_WhenComponentRemoved()
        {
            // Arrange
            var entity = world.Create();
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);

            // First apply a modifier
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableWalk = true },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, crdtEntity);
            system.Update(0);

            // Pre-assert
            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableWalk);

            // Remove the PB component
            world.Remove<PBInputModifier>(entity);

            // Act
            system.Update(0);

            // Assert
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsFalse(inputModifier.DisableWalk);

            // Should receive REMOVED action
            sceneRestrictionBusController.Received().PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.REMOVED));

            // Should remove tracking component
            Assert.IsFalse(world.Has<InputModifierComponent>(entity));
        }

        [Test]
        public void ResetModifiers_ShouldBeCalled_WhenSceneIsNotCurrent()
        {
            // Arrange
            var entity = world.Create();
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableWalk = true },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, crdtEntity);
            system.Update(0); // Apply first

            // Act
            system.OnSceneIsCurrentChanged(false);

            // Assert
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsFalse(inputModifier.DisableWalk);

            sceneRestrictionBusController.Received().PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.REMOVED));
        }

        [Test]
        public void NotApplyModifiers_WhenSceneIsNotCurrent()
        {
             // Arrange
            sceneStateProvider.IsCurrent.Returns(false);

            var entity = world.Create();
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableWalk = true },
                IsDirty = true
            };
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);

            world.Add(entity, pbInputModifier, crdtEntity);

            // Act
            system.Update(0);

            // Assert
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsFalse(inputModifier.DisableWalk);

            sceneRestrictionBusController.DidNotReceiveWithAnyArgs().PushSceneRestriction(default);
        }
    }
}

