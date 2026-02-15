using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Utils;
using Decentraland.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UiElementUtilsShould
    {
        private World world;
        private Entity entity;
        private VisualElement visualElement;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            entity = world.Create();
            visualElement = new VisualElement();
        }

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
        }

        // --- ApplyDefaultUiTransformValues tests ---

        [Test]
        public void ApplyDefaultBorderRadiusWhenNotExplicitlySet()
        {
            // Arrange
            var pbUiTransform = new PBUiTransform();

            // Act
            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, visualElement);

            // Assert
            Assert.AreEqual(new StyleLength(10f), visualElement.style.borderBottomLeftRadius);
            Assert.AreEqual(new StyleLength(10f), visualElement.style.borderBottomRightRadius);
            Assert.AreEqual(new StyleLength(10f), visualElement.style.borderTopLeftRadius);
            Assert.AreEqual(new StyleLength(10f), visualElement.style.borderTopRightRadius);
        }

        [Test]
        public void NotOverrideBorderRadiusWhenExplicitlySet()
        {
            // Arrange
            var pbUiTransform = new PBUiTransform
            {
                BorderBottomLeftRadius = 5f,
                BorderBottomRightRadius = 5f,
                BorderTopLeftRadius = 5f,
                BorderTopRightRadius = 5f,
            };

            // Pre-set the visual element with custom radius values
            visualElement.style.borderBottomLeftRadius = new StyleLength(5f);

            // Act
            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, visualElement);

            // Assert - should NOT be overridden to 10f default
            Assert.AreEqual(new StyleLength(5f), visualElement.style.borderBottomLeftRadius);
        }

        [Test]
        public void ApplyDefaultBorderWidthWhenNotExplicitlySet()
        {
            // Arrange
            var pbUiTransform = new PBUiTransform();

            // Act
            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, visualElement);

            // Assert
            Assert.AreEqual(new StyleFloat(1f), visualElement.style.borderTopWidth);
            Assert.AreEqual(new StyleFloat(1f), visualElement.style.borderRightWidth);
            Assert.AreEqual(new StyleFloat(1f), visualElement.style.borderBottomWidth);
            Assert.AreEqual(new StyleFloat(1f), visualElement.style.borderLeftWidth);
        }

        [Test]
        public void NotOverrideBorderWidthWhenExplicitlySet()
        {
            // Arrange
            var pbUiTransform = new PBUiTransform
            {
                BorderTopWidth = 3f,
                BorderRightWidth = 3f,
                BorderBottomWidth = 3f,
                BorderLeftWidth = 3f,
            };
            visualElement.style.borderTopWidth = new StyleFloat(3f);

            // Act
            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, visualElement);

            // Assert - should NOT be overridden to 1f default
            Assert.AreEqual(new StyleFloat(3f), visualElement.style.borderTopWidth);
        }

        [Test]
        public void ApplyDefaultBorderColorWhenNotExplicitlySet()
        {
            // Arrange
            var pbUiTransform = new PBUiTransform();

            // Act
            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, visualElement);

            // Assert
            Assert.AreEqual(new StyleColor(Color.gray), visualElement.style.borderTopColor);
            Assert.AreEqual(new StyleColor(Color.gray), visualElement.style.borderRightColor);
            Assert.AreEqual(new StyleColor(Color.gray), visualElement.style.borderBottomColor);
            Assert.AreEqual(new StyleColor(Color.gray), visualElement.style.borderLeftColor);
        }

        [Test]
        public void NotOverrideBorderColorWhenExplicitlySet()
        {
            // Arrange
            var pbUiTransform = new PBUiTransform
            {
                BorderTopColor = new Color4 { R = 1, G = 0, B = 0, A = 1 },
                BorderRightColor = new Color4 { R = 1, G = 0, B = 0, A = 1 },
                BorderBottomColor = new Color4 { R = 1, G = 0, B = 0, A = 1 },
                BorderLeftColor = new Color4 { R = 1, G = 0, B = 0, A = 1 },
            };
            visualElement.style.borderTopColor = new StyleColor(Color.red);

            // Act
            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, visualElement);

            // Assert - should NOT be overridden to gray default
            Assert.AreEqual(new StyleColor(Color.red), visualElement.style.borderTopColor);
        }

        [Test]
        public void AlwaysSetOverflowHidden()
        {
            // Arrange
            var pbUiTransform = new PBUiTransform();

            // Act
            UiElementUtils.ApplyDefaultUiTransformValues(in pbUiTransform, visualElement);

            // Assert
            Assert.AreEqual(Overflow.Hidden, visualElement.style.overflow.value);
        }

        // --- ApplyDefaultUiBackgroundValues tests ---

        [Test]
        public void ApplyWhiteBackgroundWhenNoPBUiBackground()
        {
            // Arrange - entity without PBUiBackground

            // Act
            UiElementUtils.ApplyDefaultUiBackgroundValues(world, entity, visualElement);

            // Assert
            Assert.AreEqual(new StyleColor(Color.white), visualElement.style.backgroundColor);
        }

        [Test]
        public void NotApplyBackgroundWhenPBUiBackgroundExists()
        {
            // Arrange - entity with PBUiBackground
            world.Add(entity, new PBUiBackground());

            // Act
            UiElementUtils.ApplyDefaultUiBackgroundValues(world, entity, visualElement);

            // Assert - should not be set to white
            Assert.AreNotEqual(new StyleColor(Color.white), visualElement.style.backgroundColor);
        }

        // --- SetupUIDropdownComponent selectedIndex tests ---

        [Test]
        public void SetupUIDropdownComponentUpdatesSelectedIndex()
        {
            // Arrange
            var dropdown = new UIDropdownComponent();
            dropdown.Initialize("TestDropdown");
            var model = new PBUiDropdown();
            model.Options.Add("Option1");
            model.Options.Add("Option2");
            model.Options.Add("Option3");
            model.SelectedIndex = 1;
            var fonts = new[] { new StyleFontDefinition() };

            // Act
            UiElementUtils.SetupUIDropdownComponent(ref dropdown, in model, in fonts);

            // Assert
            Assert.AreEqual(1, dropdown.LastIndexSetByScene);
            Assert.AreEqual("Option2", dropdown.DropdownField.value);

            dropdown.Dispose();
        }

        [Test]
        public void SetupUIDropdownComponentDoesNotReapplyWhenIndexUnchanged()
        {
            // Arrange
            var dropdown = new UIDropdownComponent();
            dropdown.Initialize("TestDropdown");
            var model = new PBUiDropdown();
            model.Options.Add("Option1");
            model.Options.Add("Option2");
            model.SelectedIndex = 0;
            var fonts = new[] { new StyleFontDefinition() };

            // First call sets the index
            UiElementUtils.SetupUIDropdownComponent(ref dropdown, in model, in fonts);
            Assert.AreEqual(0, dropdown.LastIndexSetByScene);

            // Manually change the value to simulate user interaction
            dropdown.DropdownField.SetValueWithoutNotify("Option2");

            // Act - second call with same selectedIndex should not override user selection
            UiElementUtils.SetupUIDropdownComponent(ref dropdown, in model, in fonts);

            // Assert - value should remain as user set it because selectedIndex did not change
            Assert.AreEqual("Option2", dropdown.DropdownField.value);

            dropdown.Dispose();
        }

        [Test]
        public void SetupUIDropdownComponentUsesEmptyLabelWhenIndexOutOfRange()
        {
            // Arrange
            var dropdown = new UIDropdownComponent();
            dropdown.Initialize("TestDropdown");
            var model = new PBUiDropdown { EmptyLabel = "Select..." };
            model.Options.Add("Option1");
            model.SelectedIndex = 5; // out of range
            var fonts = new[] { new StyleFontDefinition() };

            // Act
            UiElementUtils.SetupUIDropdownComponent(ref dropdown, in model, in fonts);

            // Assert - should fall back to EmptyLabel
            Assert.AreEqual("Select...", dropdown.DropdownField.value);

            dropdown.Dispose();
        }

        // --- SetupUIInputComponent tests ---

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SetupUIInputComponentDisabledState(bool disabled)
        {
            // Arrange
            var input = new UIInputComponent();
            input.Initialize(NSubstitute.Substitute.For<DCL.Input.IInputBlock>(), "TestInput", "", "", Color.gray);
            var model = new PBUiInput { Disabled = disabled };
            var fonts = new[] { new StyleFontDefinition() };

            // Act
            UiElementUtils.SetupUIInputComponent(ref input, in model, in fonts);

            // Assert
            Assert.AreEqual(disabled ? PickingMode.Ignore : PickingMode.Position, input.TextField.pickingMode);
            Assert.AreEqual(!disabled, input.TextField.enabledSelf);

            input.Dispose();
        }

        [Test]
        public void SetupUIInputComponentTextAlignOnTextElement()
        {
            // Arrange
            var input = new UIInputComponent();
            input.Initialize(NSubstitute.Substitute.For<DCL.Input.IInputBlock>(), "TestInput", "", "", Color.gray);
            var model = new PBUiInput { TextAlign = TextAlignMode.TamTopLeft };
            var fonts = new[] { new StyleFontDefinition() };

            // Act
            UiElementUtils.SetupUIInputComponent(ref input, in model, in fonts);

            // Assert - textAlign should be applied to TextElement, not TextField
            Assert.AreEqual(model.GetTextAlign(), input.TextElement.style.unityTextAlign.value);

            input.Dispose();
        }
    }
}
