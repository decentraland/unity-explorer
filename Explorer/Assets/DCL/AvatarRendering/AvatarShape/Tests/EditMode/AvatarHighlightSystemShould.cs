using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Interaction.Raycast.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarHighlightSystemShould : UnitySystemTestBase<AvatarHighlightSystem>
    {
        private const float OUTLINE_OPACITY = 0.8f;
        private const float OUTLINE_THICKNESS = 0.5f;
        private const float FADE_IN_TIME = 0.5f;
        private const float FADE_OUT_TIME = 0.3f;

        private Entity avatarEntity;

        [SetUp]
        public void Setup()
        {
            // Create mock highlight settings that control fade animation
            var highlightSettings = Substitute.For<IAvatarHighlightData>();
            highlightSettings.OutlineVfxOpacity.Returns(OUTLINE_OPACITY);
            highlightSettings.OutlineThickness.Returns(OUTLINE_THICKNESS);
            highlightSettings.OutlineColor.Returns(new Color(1, 0, 0, 1));
            highlightSettings.FadeInTimeSeconds.Returns(FADE_IN_TIME);
            highlightSettings.FadeOutTimeSeconds.Returns(FADE_OUT_TIME);

            system = new AvatarHighlightSystem(world, highlightSettings);

            var avatarShape = new AvatarShapeComponent("TestAvatar", "test-id");
            var highlightComponent = new AvatarHighlightComponent { Opacity = 0 };
            avatarEntity = world.Create(avatarShape, highlightComponent);
        }

        protected override void OnTearDown() { }

        [Test]
        public void InitializeAvatarHighlightWithZeroOpacity()
        {
            // Assert
            ref AvatarHighlightComponent highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            Assert.That(highlight.Opacity, Is.EqualTo(0));
        }

        [Test]
        public void FadeInAvatarHighlightWhenHovered()
        {
            // Arrange
            ref AvatarHighlightComponent highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            world.Add<HoveredComponent>(avatarEntity);

            // Act
            system.Update(0.1f);

            // Assert: Opacity should increase
            highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            Assert.That(highlight.Opacity, Is.GreaterThan(0));
            Assert.That(highlight.Opacity, Is.LessThanOrEqualTo(OUTLINE_OPACITY));
        }

        [Test]
        public void FadeInAvatarHighlightToMaxOpacity()
        {
            // Arrange
            world.Add<HoveredComponent>(avatarEntity);

            // Act
            system.Update(FADE_IN_TIME + 0.1f);

            // Assert: Opacity should reach maximum
            ref AvatarHighlightComponent highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            Assert.That(highlight.Opacity, Is.EqualTo(OUTLINE_OPACITY).Within(0.01f));
        }

        [Test]
        public void FadeOutAvatarHighlightWhenNotHovered()
        {
            // Arrange
            ref AvatarHighlightComponent highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            highlight.Opacity = OUTLINE_OPACITY;

            // Act
            system.Update(0.1f);

            // Assert: Opacity should decrease
            highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            Assert.That(highlight.Opacity, Is.GreaterThan(0));
            Assert.That(highlight.Opacity, Is.LessThanOrEqualTo(OUTLINE_OPACITY));
        }

        [Test]
        public void FadeOutAvatarHighlightToZero()
        {
            // Arrange
            ref AvatarHighlightComponent highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            highlight.Opacity = OUTLINE_OPACITY;

            // Act
            system.Update(FADE_OUT_TIME + 0.1f);

            // Assert: Opacity should reach zero
            highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            Assert.That(highlight.Opacity, Is.EqualTo(0).Within(0.01f));
        }

        [Test]
        public void StopFadingWhenAlreadyAtTargetOpacity()
        {
            // Arrange
            ref AvatarHighlightComponent highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            highlight.Opacity = OUTLINE_OPACITY;
            world.Add<HoveredComponent>(avatarEntity);

            // Act
            system.Update(0.1f);

            // Assert: Opacity shouldn't go higher than OUTLINE_OPACITY
            highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            Assert.That(highlight.Opacity, Is.EqualTo(OUTLINE_OPACITY).Within(0.01f));
        }

        [Test]
        public void NotFadeWhenOpacityIsZeroAndNotHovered()
        {
            // Arrange
            ref AvatarHighlightComponent highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            Assert.That(highlight.Opacity, Is.EqualTo(0));

            // Act
            system.Update(0.1f);

            // Assert: Opacity should remain 0
            highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            Assert.That(highlight.Opacity, Is.EqualTo(0));
        }

        [Test]
        public void UseCorrectFadeInTimeForCalculations()
        {
            // Arrange
            world.Add<HoveredComponent>(avatarEntity);
            float expectedStepFadeIn = OUTLINE_OPACITY / FADE_IN_TIME;
            var stepTime = 0.1f;

            // Act
            system.Update(stepTime);

            // Assert: Outline opacity increase should match the step for the given time
            ref AvatarHighlightComponent highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            float expectedOpacity = Mathf.MoveTowards(0, OUTLINE_OPACITY, expectedStepFadeIn * stepTime);
            Assert.That(highlight.Opacity, Is.EqualTo(expectedOpacity).Within(0.001f));
        }

        [Test]
        public void UseCorrectFadeOutTimeForCalculations()
        {
            // Arrange
            ref AvatarHighlightComponent highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            highlight.Opacity = OUTLINE_OPACITY;
            float expectedStepFadeOut = OUTLINE_OPACITY / FADE_OUT_TIME;
            var stepTime = 0.1f;

            // Act
            system.Update(stepTime);

            // Assert: Outline opacity increase should match the step for the given time
            highlight = ref world.Get<AvatarHighlightComponent>(avatarEntity);
            float expectedOpacity = Mathf.MoveTowards(OUTLINE_OPACITY, 0, expectedStepFadeOut * stepTime);
            Assert.That(highlight.Opacity, Is.EqualTo(expectedOpacity).Within(0.001f));
        }
    }
}
