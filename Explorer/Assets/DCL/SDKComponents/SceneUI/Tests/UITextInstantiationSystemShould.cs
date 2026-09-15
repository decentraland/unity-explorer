using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Systems.UIText;
using DCL.SDKComponents.SceneUI.Utils;
using Decentraland.Common;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Fonts;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITextInstantiationSystemShould : UnitySystemTestBase<UITextInstantiationSystem>
    {
        private IComponentPoolsRegistry poolsRegistry;
        private ISceneData sceneData;
        private Entity entity;
        private UITransformComponent uiTransformComponent;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(Label), new ComponentPool.WithDefaultCtor<Label>() },
                }, null);

            sceneData = Substitute.For<ISceneData>();
            system = new UITextInstantiationSystem(world, poolsRegistry, new []{new StyleFontDefinition()}, sceneData, PartitionComponent.TOP_PRIORITY);
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
        public void WrapTextWhenTextWrapIsAbsent()
        {
            // Arrange
            var input = new PBUiText();
            world.Add(entity, input);
            system.Update(0);

            // Act
            input.IsDirty = true;
            system.Update(0);

            // Assert
            ref UITextComponent uiTextComponent = ref world.Get<UITextComponent>(entity);
            Assert.AreEqual(WhiteSpace.Normal, uiTextComponent.Label.style.whiteSpace.value);
        }

        [Test]
        public void NotWrapTextWhenTextWrapIsExplicitlyNoWrap()
        {
            // Arrange
            var input = new PBUiText();
            world.Add(entity, input);
            system.Update(0);

            // Act
            input.TextWrap = TextWrap.TwNoWrap;
            input.IsDirty = true;
            system.Update(0);

            // Assert
            ref UITextComponent uiTextComponent = ref world.Get<UITextComponent>(entity);
            Assert.AreEqual(WhiteSpace.NoWrap, uiTextComponent.Label.style.whiteSpace.value);
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
        public void RequestFontWhenFontSrcIsAContentFile()
        {
            var fontUrl = URLAddress.FromString("https://peer.decentraland.org/content/contents/bafyfont");
            sceneData.TryGetMediaUrl("fonts/Lobster-Regular.ttf", out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = fontUrl;
                          return true;
                      });

            var input = new PBUiText { FontSrc = "fonts/Lobster-Regular.ttf", IsDirty = true };
            world.Add(entity, input);

            system.Update(0);

            ref UITextComponent uiTextComponent = ref world.Get<UITextComponent>(entity);
            Assert.That(uiTextComponent.FontRequest.Src, Is.EqualTo("fonts/Lobster-Regular.ttf"));
            Assert.That(uiTextComponent.FontRequest.Promise, Is.Not.Null);

            GetFontIntention intention = world.Get<GetFontIntention>(uiTextComponent.FontRequest.Promise!.Value.Entity);
            Assert.That(intention.Kind, Is.EqualTo(FontSourceKind.File));
            Assert.That(intention.CommonArguments.URL, Is.EqualTo(fontUrl));
        }

        [Test]
        public void KeepBuiltInFontWhenFontSrcIsEmpty()
        {
            var input = new PBUiText();
            world.Add(entity, input);

            system.Update(0);

            ref UITextComponent uiTextComponent = ref world.Get<UITextComponent>(entity);
            Assert.That(uiTextComponent.FontRequest.Src, Is.Null);
            Assert.That(uiTextComponent.FontRequest.Promise, Is.Null);
            Assert.That(uiTextComponent.CustomFont, Is.Null);
        }

        [Test]
        public void ReplaceRequestWhenFontSrcChanges()
        {
            var input = new PBUiText { FontSrc = "Roboto", IsDirty = true };
            world.Add(entity, input);
            system.Update(0);
            Entity firstPromiseEntity = world.Get<UITextComponent>(entity).FontRequest.Promise!.Value.Entity;

            input.FontSrc = "Lobster";
            input.IsDirty = true;
            system.Update(0);

            ref UITextComponent uiTextComponent = ref world.Get<UITextComponent>(entity);
            Assert.That(world.IsAlive(firstPromiseEntity), Is.False);
            Assert.That(uiTextComponent.FontRequest.Src, Is.EqualTo("Lobster"));
            Assert.That(world.Get<GetFontIntention>(uiTextComponent.FontRequest.Promise!.Value.Entity).Kind, Is.EqualTo(FontSourceKind.FontsourceFamily));
        }
    }
}
