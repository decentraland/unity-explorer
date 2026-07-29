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

            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.Applied));

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

            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.Applied));
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
            sceneRestrictionBusController.Received().PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.Removed));

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

            sceneRestrictionBusController.Received().PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.Removed));
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

            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.Applied));
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

            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.Removed));
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
            sceneRestrictionBusController.Received(1).PushSceneRestriction(Arg.Is<SceneRestriction>(r => r.Action == SceneRestrictionsAction.Applied));
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

        [Test]
        public void FinalizeComponents_NoOp_WhenSceneNeverApplied()
        {
            // In multi-scene worlds, a scene that never applied a modifier
            // must not reset the shared global on its own teardown, or it would clobber
            // a modifier set by a different (still-running) scene.
            ref InputModifierComponent inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
            inputModifier.DisableAll = true;
            sceneRestrictionBusController.ClearReceivedCalls();

            system.FinalizeComponents(default);

            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableAll);
            sceneRestrictionBusController.DidNotReceiveWithAnyArgs().PushSceneRestriction(default);
        }

        [Test]
        public void OnSceneIsCurrentChanged_False_NoOp_WhenSceneNeverApplied()
        {
            // A scene that never applied a modifier must not reset the
            // shared global when it simply transitions to non-current.
            ref InputModifierComponent inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
            inputModifier.DisableAll = true;
            sceneRestrictionBusController.ClearReceivedCalls();

            system.OnSceneIsCurrentChanged(false);

            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableAll);
            sceneRestrictionBusController.DidNotReceiveWithAnyArgs().PushSceneRestriction(default);
        }

        [Test]
        public void ResetGliding_WhenLeavingScene_ThatOnlyDisabledGliding()
        {
            // Repro: a scene disables only gliding, then the player teleports away.
            // The shared global flag must be cleared when the scene stops being current,
            // otherwise the glider stays disabled in every subsequent scene until a client restart.
            var entity = world.Create();
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableGliding = true },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, crdtEntity);
            system.Update(0); // Apply first

            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableGliding);

            // Act - scene stops being current (teleport to another scene)
            system.OnSceneIsCurrentChanged(false);

            // Assert - global gliding restriction must be lifted
            Assert.IsFalse(globalWorld.Get<InputModifierComponent>(playerEntity).DisableGliding);
        }

        [Test]
        public void ResetDoubleJump_WhenLeavingScene_ThatOnlyDisabledDoubleJump()
        {
            // Same stale-state defect as gliding: a scene disabling only double jump
            // must not leak that restriction into the next scene.
            var entity = world.Create();
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableDoubleJump = true },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, crdtEntity);
            system.Update(0); // Apply first

            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableDoubleJump);

            // Act - scene stops being current (teleport to another scene)
            system.OnSceneIsCurrentChanged(false);

            // Assert - global double-jump restriction must be lifted
            Assert.IsFalse(globalWorld.Get<InputModifierComponent>(playerEntity).DisableDoubleJump);
        }

        [Test]
        public void NotPushMovementBlockedBus_WhenOnlyGlidingDisabled()
        {
            // Gliding is deliberately excluded from the AvatarMovementsBlocked bus indicator.
            // Pins that intended UX and guards against re-coupling the reset logic to the bus
            // (which is exactly what caused the stale-glider bug).
            var entity = world.Create();
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableGliding = true },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));

            system.Update(0);

            sceneRestrictionBusController.DidNotReceiveWithAnyArgs().PushSceneRestriction(default);
        }

        [Test]
        public void NotPushMovementBlockedBus_WhenOnlyDoubleJumpDisabled()
        {
            var entity = world.Create();
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableDoubleJump = true },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));

            system.Update(0);

            sceneRestrictionBusController.DidNotReceiveWithAnyArgs().PushSceneRestriction(default);
        }

        [Test]
        public void ReapplyGliding_WhenSceneBecomesCurrentAgain()
        {
            // Symmetric to the reset-on-leave case: returning to the scene must re-assert its gliding block.
            var entity = world.Create();
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableGliding = true },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, crdtEntity);
            system.Update(0);

            system.OnSceneIsCurrentChanged(false);
            Assert.IsFalse(globalWorld.Get<InputModifierComponent>(playerEntity).DisableGliding);

            // Act - back to this scene
            system.OnSceneIsCurrentChanged(true);

            // Assert
            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableGliding);
        }

        [Test]
        public void ClearGate_WhenSceneReplacesRestrictionWithEmptyModifier()
        {
            // A scene that first disables gliding then updates itself to an empty modifier
            // no longer asserts anything, so a later teardown must be a no-op.
            var entity = world.Create();
            var crdtEntity = new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY);
            var pbInputModifier = new PBInputModifier
            {
                Standard = new PBInputModifier.Types.StandardInput { DisableGliding = true },
                IsDirty = true
            };
            world.Add(entity, pbInputModifier, crdtEntity);
            system.Update(0);
            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).DisableGliding);

            // Scene clears its own modifier
            PBInputModifier pb = world.Get<PBInputModifier>(entity);
            pb.Standard.DisableGliding = false;
            pb.IsDirty = true;
            system.Update(0);
            Assert.IsFalse(globalWorld.Get<InputModifierComponent>(playerEntity).DisableGliding);

            // Teardown must not touch the global anymore
            var globalBefore = globalWorld.Get<InputModifierComponent>(playerEntity);
            system.FinalizeComponents(default);
            Assert.AreEqual(globalBefore.EverythingEnabled, globalWorld.Get<InputModifierComponent>(playerEntity).EverythingEnabled);
            Assert.IsTrue(globalWorld.Get<InputModifierComponent>(playerEntity).EverythingEnabled);
        }
    }
}

