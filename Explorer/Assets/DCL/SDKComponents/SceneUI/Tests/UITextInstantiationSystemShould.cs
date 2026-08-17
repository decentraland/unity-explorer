using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIText;
using DCL.SDKComponents.SceneUI.Utils;
using Decentraland.Common;
using ECS.TestSuite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITextInstantiationSystemShould : UnitySystemTestBase<UITextInstantiationSystem>
    {
        private IComponentPoolsRegistry poolsRegistry = null!;
        private Entity entity;
        private UITransformComponent uiTransformComponent = null!;
        private StyleFontDefinition[] fonts = null!;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(Label), new ComponentPool.WithDefaultCtor<Label>() },
                }, null!);

            fonts = new[] { new StyleFontDefinition() };
            system = new UITextInstantiationSystem(world, poolsRegistry, fonts, wrapUnsetTextByDefault: true);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
        }

        [Test]
        public void InstantiateUIText()
        {
            // Arrange
            var input = new PBUiText();

            // Act
            world.Add(entity, input);
            system.Update(0);

            // Assert
            ref UITextComponent uiTextComponent = ref world.Get<UITextComponent>(entity);
            Assert.IsNotNull(uiTextComponent.Label);
            Assert.AreEqual(UiElementUtils.BuildElementName("UIText", entity), uiTextComponent.Label.name);
            Assert.AreEqual(PickingMode.Ignore, uiTextComponent.Label.pickingMode);
            Assert.IsTrue(uiTransformComponent.Transform.Contains(uiTextComponent.Label));
        }

        [Test]
        public void UpdateUIText()
        {
            // Arrange
            var input = new PBUiText();
            world.Add(entity, input);
            system.Update(0);
            const int NUMBER_OF_UPDATES = 3;

            for (var i = 0; i < NUMBER_OF_UPDATES; i++)
            {
                // Act
                input.Value = $"Test text {i}";
                input.Color = new Color4 { R = i, G = 1, B = 1, A = 1 };
                input.FontSize = i + 1;
                input.TextAlign = (TextAlignMode) i;
                input.IsDirty = true;
                system.Update(0);

                // Assert
                ref UITextComponent uiTextComponent = ref world.Get<UITextComponent>(entity);
                Assert.AreEqual(input.Value, uiTextComponent.Label.text);
                Assert.IsTrue(input.GetColor() == uiTextComponent.Label.style.color);
                Assert.IsTrue(input.GetFontSize() == uiTextComponent.Label.style.fontSize);
                Assert.IsTrue(input.GetTextAlign() == uiTextComponent.Label.style.unityTextAlign);
            }
        }

        [Test]
        public void WrapUnsetTextWrapForNewScenes()
        {
            // Arrange
            var label = new Label();
            var model = new PBUiText();
            Assert.IsFalse(model.HasTextWrap);

            // Act
            UiElementUtils.SetupLabel(ref label, ref model, ref uiTransformComponent, in fonts, wrapUnsetByDefault: true);

            // Assert
            Assert.AreEqual(WhiteSpace.Normal, label.style.whiteSpace.value);
        }

        [Test]
        public void KeepUnsetTextWrapAsNoWrapForLegacyScenes()
        {
            // Arrange
            var label = new Label();
            var model = new PBUiText();
            Assert.IsFalse(model.HasTextWrap);

            // Act
            UiElementUtils.SetupLabel(ref label, ref model, ref uiTransformComponent, in fonts, wrapUnsetByDefault: false);

            // Assert
            Assert.AreEqual(WhiteSpace.NoWrap, label.style.whiteSpace.value);
        }

        [Test]
        public void HonorExplicitTextWrapRegardlessOfDefault([Values(false, true)] bool wrapUnsetByDefault)
        {
            // Arrange
            var wrapLabel = new Label();
            var wrapModel = new PBUiText { TextWrap = TextWrap.TwWrap };
            var noWrapLabel = new Label();
            var noWrapModel = new PBUiText { TextWrap = TextWrap.TwNoWrap };

            // Act
            UiElementUtils.SetupLabel(ref wrapLabel, ref wrapModel, ref uiTransformComponent, in fonts, wrapUnsetByDefault);
            UiElementUtils.SetupLabel(ref noWrapLabel, ref noWrapModel, ref uiTransformComponent, in fonts, wrapUnsetByDefault);

            // Assert
            Assert.AreEqual(WhiteSpace.Normal, wrapLabel.style.whiteSpace.value);
            Assert.AreEqual(WhiteSpace.NoWrap, noWrapLabel.style.whiteSpace.value);
        }

        [Test]
        public void WrapUnsetTextForLocalSceneDevelopmentDespiteOldTimestamp()
        {
            // Arrange
            bool wrapUnsetByDefault = SceneUIPlugin.ShouldWrapUnsetTextByDefault(isLocalSceneDevelopment: true, sceneDeployTimestampMs: 0);
            Assert.IsTrue(wrapUnsetByDefault);
            Assert.IsFalse(SceneUIPlugin.ShouldWrapUnsetTextByDefault(isLocalSceneDevelopment: false, sceneDeployTimestampMs: 0));

            var label = new Label();
            var model = new PBUiText();
            Assert.IsFalse(model.HasTextWrap);

            // Act
            UiElementUtils.SetupLabel(ref label, ref model, ref uiTransformComponent, in fonts, wrapUnsetByDefault);

            // Assert
            Assert.AreEqual(WhiteSpace.Normal, label.style.whiteSpace.value);
        }

        [Test]
        public void WrapUnsetTextForScenesDeployedAfterTheDefaultChanged()
        {
            // Arrange
            long recentDeployTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Act
            bool wrapUnsetByDefault = SceneUIPlugin.ShouldWrapUnsetTextByDefault(isLocalSceneDevelopment: false, recentDeployTimestampMs);

            // Assert
            Assert.IsTrue(wrapUnsetByDefault);
        }
    }
}
