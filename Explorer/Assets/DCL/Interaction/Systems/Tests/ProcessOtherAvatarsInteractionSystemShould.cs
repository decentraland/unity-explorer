using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.CharacterCamera.Components;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Input.Component;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.Profiles;
using DCL.Utilities;
using ECS.TestSuite;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.InputSystem;

namespace DCL.Interaction.Systems.Tests
{
    [TestFixture]
    public class ProcessOtherAvatarsInteractionSystemShould : InputTestFixture
    {
        private const string TEST_USER_ID = "0xtestuser1234";

        private ProcessOtherAvatarsInteractionSystem system;
        private World world;
        private Mouse mouse;
        private IEventSystem eventSystem;
        private IMVCManagerMenusAccessFacade menusAccessFacade;
        private IMVCManager mvcManager;
        private ObjectProxy<Entity> cameraEntityProxy;
        private Entity cameraEntity;
        private Entity queryEntity;
        private Entity avatarEntity;
        private Profile testProfile;

        private void SetUpWithFeatureFlag(bool enableContextMenu)
        {
            base.Setup();

            EcsTestsUtils.SetUpFeaturesRegistry();
            OverrideFeatureFlag(FeatureId.AVATAR_CONTEXT_MENU, enableContextMenu);

            world = World.Create();
            mouse = InputSystem.AddDevice<Mouse>();

            DCLInput.Instance.Enable();

            eventSystem = Substitute.For<IEventSystem>();
            menusAccessFacade = Substitute.For<IMVCManagerMenusAccessFacade>();
            mvcManager = Substitute.For<IMVCManager>();

            cameraEntity = world.Create(
                new CursorComponent { CursorState = CursorState.Locked }
            );

            cameraEntityProxy = new ObjectProxy<Entity>();
            cameraEntityProxy.SetObject(cameraEntity);

            testProfile = new Profile { UserId = TEST_USER_ID };
            avatarEntity = world.Create(testProfile);

            queryEntity = world.Create(
                new PlayerOriginRaycastResultForGlobalEntities(),
                new HoverFeedbackComponent(4),
                new HoverStateComponent()
            );

            system = new ProcessOtherAvatarsInteractionSystem(
                world, eventSystem, menusAccessFacade, mvcManager, cameraEntityProxy);
        }

        [TearDown]
        public override void TearDown()
        {
            system?.Dispose();
            world?.Dispose();

            if (mouse != null)
                InputSystem.RemoveDevice(mouse);

            EcsTestsUtils.TearDownFeaturesRegistry();
            base.TearDown();
        }

        private static void OverrideFeatureFlag(FeatureId featureId, bool value)
        {
            FieldInfo field = typeof(FeaturesRegistry).GetField("featureStates", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (Dictionary<FeatureId, bool>)field!.GetValue(FeaturesRegistry.Instance);
            dict[featureId] = value;
        }

        private void SetupValidAvatarHit()
        {
            ref var raycast = ref world.Get<PlayerOriginRaycastResultForGlobalEntities>(queryEntity);
            raycast.SetupHit(default, new GlobalColliderGlobalEntityInfo(avatarEntity), 5f);
        }

        private void ClearRaycastHit()
        {
            ref var raycast = ref world.Get<PlayerOriginRaycastResultForGlobalEntities>(queryEntity);
            raycast.Reset();
        }

        private int CountContextMenuCalls() =>
            menusAccessFacade.ReceivedCalls()
                .Count(c => c.GetMethodInfo().Name == nameof(IMVCManagerMenusAccessFacade.ShowUserProfileContextMenuFromWalletIdAsync));

        // ==========================================================
        // Hover feedback tests (shared behavior for both modes)
        // ==========================================================

        [TestCase(true)]
        [TestCase(false)]
        public void AddTooltipWhenHoveringValidAvatar(bool contextMenuEnabled)
        {
            // Arrange
            SetUpWithFeatureFlag(contextMenuEnabled);
            SetupValidAvatarHit();

            // Act
            system.Update(0);

            // Assert
            ref var feedback = ref world.Get<HoverFeedbackComponent>(queryEntity);
            Assert.That(feedback.Enabled, Is.True);
            Assert.That(feedback.Tooltips.Count, Is.EqualTo(1));

            string expectedText = contextMenuEnabled ? "Options" : "View Profile";
            Assert.That(feedback.Tooltips[0].Text, Is.EqualTo(expectedText));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ClearTooltipWhenNoValidHit(bool contextMenuEnabled)
        {
            // Arrange
            SetUpWithFeatureFlag(contextMenuEnabled);
            SetupValidAvatarHit();
            system.Update(0);

            ClearRaycastHit();

            // Act
            system.Update(0);

            // Assert
            ref var feedback = ref world.Get<HoverFeedbackComponent>(queryEntity);
            Assert.That(feedback.Enabled, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ClearTooltipWhenPointerOverUI(bool contextMenuEnabled)
        {
            // Arrange
            SetUpWithFeatureFlag(contextMenuEnabled);
            SetupValidAvatarHit();
            eventSystem.IsPointerOverGameObject().Returns(true);

            // Act
            system.Update(0);

            // Assert
            ref var feedback = ref world.Get<HoverFeedbackComponent>(queryEntity);
            Assert.That(feedback.Enabled, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void IgnoreHiddenPlayers(bool contextMenuEnabled)
        {
            // Arrange
            SetUpWithFeatureFlag(contextMenuEnabled);
            world.Add(avatarEntity, new HiddenPlayerComponent());
            SetupValidAvatarHit();

            // Act
            system.Update(0);

            // Assert
            ref var feedback = ref world.Get<HoverFeedbackComponent>(queryEntity);
            Assert.That(feedback.Enabled, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void IgnoreEntitiesWithIgnoreInteraction(bool contextMenuEnabled)
        {
            // Arrange
            SetUpWithFeatureFlag(contextMenuEnabled);
            world.Add(avatarEntity, new IgnoreInteractionComponent());
            SetupValidAvatarHit();

            // Act
            system.Update(0);

            // Assert
            ref var feedback = ref world.Get<HoverFeedbackComponent>(queryEntity);
            Assert.That(feedback.Enabled, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void IgnoreEntitiesWithoutProfile(bool contextMenuEnabled)
        {
            // Arrange
            SetUpWithFeatureFlag(contextMenuEnabled);

            Entity entityWithoutProfile = world.Create();
            ref var raycast = ref world.Get<PlayerOriginRaycastResultForGlobalEntities>(queryEntity);
            raycast.SetupHit(default, new GlobalColliderGlobalEntityInfo(entityWithoutProfile), 5f);

            // Act
            system.Update(0);

            // Assert
            ref var feedback = ref world.Get<HoverFeedbackComponent>(queryEntity);
            Assert.That(feedback.Enabled, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AssignHoverColliderOnValidHit(bool contextMenuEnabled)
        {
            // Arrange
            SetUpWithFeatureFlag(contextMenuEnabled);
            SetupValidAvatarHit();

            // Act
            system.Update(0);

            // Assert
            ref var hoverState = ref world.Get<HoverStateComponent>(queryEntity);
            Assert.That(hoverState.HasCollider, Is.True);
            Assert.That(hoverState.IsAtDistance, Is.True);
            Assert.That(hoverState.IsCursorInteraction, Is.True);
        }

        // ==========================================================
        // Non-context-menu mode (feature flag OFF) - left click
        // ==========================================================

        [Test]
        public void OpenPassportOnLeftClickWhenHoveringAvatar()
        {
            // Arrange
            SetUpWithFeatureFlag(false);
            SetupValidAvatarHit();
            system.Update(0);

            // Act - release triggers passport (IsPressed check filters out the press)
            Press(mouse.leftButton);
            Release(mouse.leftButton);

            // Assert
            Assert.That(mvcManager.ReceivedCalls().Count(), Is.EqualTo(1));
        }

        [Test]
        public void NotOpenPassportWhenNoAvatarHovered()
        {
            // Arrange
            SetUpWithFeatureFlag(false);
            system.Update(0);

            // Act
            Press(mouse.leftButton);
            Release(mouse.leftButton);

            // Assert
            Assert.That(mvcManager.ReceivedCalls(), Is.Empty);
        }

        [Test]
        public void NotOpenPassportWhenUserIdIsEmpty()
        {
            // Arrange
            SetUpWithFeatureFlag(false);
            testProfile.UserId = "";
            SetupValidAvatarHit();
            system.Update(0);

            // Act
            Press(mouse.leftButton);
            Release(mouse.leftButton);

            // Assert
            Assert.That(mvcManager.ReceivedCalls(), Is.Empty);
        }

        // ==========================================================
        // Context-menu mode (feature flag ON) - right click
        // ==========================================================

        [Test]
        public void OpenContextMenuOnRightClickWhenHoveringAvatar()
        {
            // Arrange
            SetUpWithFeatureFlag(true);
            SetupValidAvatarHit();
            system.Update(0);

            // Act - context menu opens on press (not release)
            Press(mouse.rightButton);

            // Assert
            Assert.That(CountContextMenuCalls(), Is.EqualTo(1));
        }

        [Test]
        public void NotOpenContextMenuWhenNoAvatarHovered()
        {
            // Arrange
            SetUpWithFeatureFlag(true);
            system.Update(0);

            // Act
            Press(mouse.rightButton);

            // Assert
            Assert.That(CountContextMenuCalls(), Is.EqualTo(0));
        }

        [Test]
        public void NotOpenContextMenuWhenUserIdIsEmpty()
        {
            // Arrange
            SetUpWithFeatureFlag(true);
            testProfile.UserId = "";
            SetupValidAvatarHit();
            system.Update(0);

            // Act
            Press(mouse.rightButton);

            // Assert
            Assert.That(CountContextMenuCalls(), Is.EqualTo(0));
        }

        [Test]
        public void SetPointerLockIntentionWithUIWhenCursorLockedAndRightClick()
        {
            // Arrange
            SetUpWithFeatureFlag(true);
            world.Set(cameraEntity, new CursorComponent { CursorState = CursorState.Locked });
            SetupValidAvatarHit();
            system.Update(0);

            // Act
            Press(mouse.rightButton);

            // Assert
            Assert.That(world.Has<PointerLockIntention>(cameraEntity), Is.True);

            var intention = world.Get<PointerLockIntention>(cameraEntity);
            Assert.That(intention.Locked, Is.True);
            Assert.That(intention.WithUI, Is.True);
        }

        [Test]
        public void NotSetPointerLockIntentionWhenCursorFreeAndRightClick()
        {
            // Arrange
            SetUpWithFeatureFlag(true);
            world.Set(cameraEntity, new CursorComponent { CursorState = CursorState.Free });
            SetupValidAvatarHit();
            system.Update(0);

            // Act
            Press(mouse.rightButton);

            // Assert
            Assert.That(world.Has<PointerLockIntention>(cameraEntity), Is.False);
        }

        // ==========================================================
        // Dispose / cleanup
        // ==========================================================

        [Test]
        public void DisposeWithoutErrorsInContextMenuMode()
        {
            // Arrange
            SetUpWithFeatureFlag(true);

            // Act & Assert
            Assert.DoesNotThrow(() => system.Dispose());
            system = null;
        }

        [Test]
        public void DisposeWithoutErrorsInNonContextMenuMode()
        {
            // Arrange
            SetUpWithFeatureFlag(false);

            // Act & Assert
            Assert.DoesNotThrow(() => system.Dispose());
            system = null;
        }
    }
}
