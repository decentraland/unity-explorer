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
        public void ApplyAllModifiers_WhenDisableAllIsTrue()
        {
            // Arrange
            var entity = world.Create();
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput
                {
                    DisableAll = true
                },
                IsDirty = true
            };

            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);
            world.Add(entity, pbInputModifier, crdtEntity);

            // Act
            system.Update(0);

            // Assert - All individual flags should be disabled when DisableAll is true
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsTrue(inputModifier.DisableAll);
            Assert.IsTrue(inputModifier.DisableWalk);
            Assert.IsTrue(inputModifier.DisableJog);
            Assert.IsTrue(inputModifier.DisableRun);
            Assert.IsTrue(inputModifier.DisableJump);
            Assert.IsTrue(inputModifier.DisableEmote);

            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.APPLIED));
        }

        [Test]
        public void ApplyIndividualModifiers_WhenDisableAllIsFalse()
        {
            // Arrange
            var entity = world.Create();
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput
                {
                    DisableAll = false,
                    DisableWalk = true,
                    DisableJog = true,
                    DisableRun = false,
                    DisableJump = true,
                    DisableEmote = true
                },
                IsDirty = true
            };

            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);
            world.Add(entity, pbInputModifier, crdtEntity);

            // Act
            system.Update(0);

            // Assert - Individual flags should be applied
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsFalse(inputModifier.DisableAll);
            Assert.IsTrue(inputModifier.DisableWalk);
            Assert.IsTrue(inputModifier.DisableJog);
            Assert.IsFalse(inputModifier.DisableRun);
            Assert.IsTrue(inputModifier.DisableJump);
            Assert.IsTrue(inputModifier.DisableEmote);
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

        [Test]
        public void NotApplyModifiers_WhenEntityIsNotPlayer()
        {
            // Arrange
            var entity = world.Create();
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableWalk = true },
                IsDirty = true
            };
            var crdtEntity = new CRDTEntity(999); // Not a player entity

            world.Add(entity, pbInputModifier, crdtEntity);

            // Act
            system.Update(0);

            // Assert
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsFalse(inputModifier.DisableWalk);
            Assert.IsFalse(world.Has<InputModifierComponent>(entity));

            sceneRestrictionBusController.DidNotReceiveWithAnyArgs().PushSceneRestriction(default);
        }

        [Test]
        public void NotApplyModifiers_WhenNotDirty()
        {
            // Arrange
            var entity = world.Create();
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableWalk = true },
                IsDirty = false // Not dirty
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

        [Test]
        public void NotApplyModifiers_WhenModeIsNone()
        {
            // Arrange
            var entity = world.Create();
            var pbInputModifier = new PBInputModifier
            {
                // No Standard set, so ModeCase is None
                IsDirty = true
            };
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);

            world.Add(entity, pbInputModifier, crdtEntity);

            // Act
            system.Update(0);

            // Assert
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsFalse(inputModifier.DisableWalk);
            Assert.IsFalse(world.Has<InputModifierComponent>(entity));

            sceneRestrictionBusController.DidNotReceiveWithAnyArgs().PushSceneRestriction(default);
        }

        [Test]
        public void ReapplyModifiers_WhenSceneBecomesCurrent()
        {
            // Arrange
            var entity = world.Create();
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableWalk = true, DisableJump = true },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, crdtEntity);
            system.Update(0); // Apply first

            // Scene becomes not current
            system.OnSceneIsCurrentChanged(false);
            Assert.IsFalse(globalWorld.Get<InputModifierComponent>(playerEntity).DisableWalk);

            sceneRestrictionBusController.ClearReceivedCalls();

            // Act - Scene becomes current again
            system.OnSceneIsCurrentChanged(true);

            // Assert - Modifiers should be reapplied
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsTrue(inputModifier.DisableWalk);
            Assert.IsTrue(inputModifier.DisableJump);

            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.APPLIED));
        }

        [Test]
        public void ResetAllModifiers_WhenFinalizeComponentsCalled()
        {
            // Arrange
            var entity = world.Create();
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput
                {
                    DisableAll = true
                },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, crdtEntity);
            system.Update(0); // Apply first

            // Pre-assert
            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableAll);

            sceneRestrictionBusController.ClearReceivedCalls();

            // Act
            system.FinalizeComponents(default);

            // Assert - All modifiers should be reset
            var inputModifier = globalWorld.Get<InputModifierComponent>(playerEntity);
            Assert.IsFalse(inputModifier.DisableAll);
            Assert.IsFalse(inputModifier.DisableWalk);
            Assert.IsFalse(inputModifier.DisableJog);
            Assert.IsFalse(inputModifier.DisableRun);
            Assert.IsFalse(inputModifier.DisableJump);
            Assert.IsFalse(inputModifier.DisableEmote);

            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.REMOVED));
        }

        [Test]
        public void NotSendDuplicateBusMessages_WhenStateUnchanged()
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

            // Act - Apply twice
            system.Update(0);
            world.Get<PBInputModifier>(entity).IsDirty = true; // Mark dirty again
            system.Update(0);

            // Assert - Should only receive one APPLIED message (deduplicated)
            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.APPLIED));
        }

        [Test]
        public void NotRemoveTrackingComponent_WhenEntityIsNotPlayer()
        {
            // Arrange - Create a non-player entity with InputModifierComponent but no PBInputModifier
            var entity = world.Create();
            var crdtEntity = new CRDTEntity(999); // Not a player entity
            world.Add(entity, new InputModifierComponent(), crdtEntity);

            // Act
            system.Update(0);

            // Assert - Should not remove the component since it's not a player entity
            Assert.IsTrue(world.Has<InputModifierComponent>(entity));
        }
    }
}

