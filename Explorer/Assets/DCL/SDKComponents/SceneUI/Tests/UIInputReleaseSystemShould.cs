using DCL.ECSComponents;
using DCL.Input;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIInput;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;
using Font = DCL.ECSComponents.Font;
using QueryDescription = Arch.Core.QueryDescription;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIInputReleaseSystemShould : UnitySystemTestBase<UIInputReleaseSystem>
    {
        private const string FONT_SRC = "Roboto";

        private IComponentPool componentPool = null!;
        private FontAsset customFont = null!;
        private UIInputComponent component = null!;
        private Entity entity;
        private Entity promiseEntity;

        [SetUp]
        public void SetUp()
        {
            componentPool = Substitute.For<IComponentPool>();
            var poolsRegistry = new ComponentPoolsRegistry(new Dictionary<Type, IComponentPool> { { typeof(UIInputComponent), componentPool } }, null);
            system = new UIInputReleaseSystem(world, poolsRegistry);

            customFont = TestFonts.CreateUIToolkitFont();

            component = new UIInputComponent();
            component.Initialize(Substitute.For<IInputBlock>(), "input", string.Empty, string.Empty, Color.white);
            component.CustomFont = customFont;
            UiElementUtils.SetFont(component.TextField, Font.FSansSerif, new[] { new StyleFontDefinition() }, customFont);
            component.FontRequest.Update(world, Substitute.For<ISceneData>(), FONT_SRC, PartitionComponent.TOP_PRIORITY);
            promiseEntity = component.FontRequest.Promise!.Value.Entity;

            entity = world.Create(new PBUiInput { FontSrc = FONT_SRC }, component);
        }

        protected override void OnTearDown()
        {
            UnityEngine.Object.DestroyImmediate(customFont);
        }

        [Test]
        public void ReturnTheComponentToThePoolWhenItIsRemoved()
        {
            world.Remove<PBUiInput>(entity);

            system.Update(0);

            Assert.That(world.Has<UIInputComponent>(entity), Is.False);
            componentPool.Received(1).Release(component);
            AssertFontReleased();
        }

        [Test]
        public void ReturnTheComponentToThePoolWhenTheEntityIsDeleted()
        {
            world.Add(entity, new DeleteEntityIntention());

            system.Update(0);

            Assert.That(world.Has<UIInputComponent>(entity), Is.False);
            componentPool.Received(1).Release(component);
            AssertFontReleased();
        }

        [Test]
        public void ReleaseEveryFontWhenTheWorldIsFinalized()
        {
            system.FinalizeComponents(world.Query(QueryDescription.Null));

            componentPool.DidNotReceiveWithAnyArgs().Release(default!);
            AssertFontReleased();
        }

        private void AssertFontReleased()
        {
            Assert.That(component.CustomFont, Is.Null);
            Assert.That(component.FontRequest.Promise, Is.Null);
            Assert.That(world.IsAlive(promiseEntity), Is.False);
            Assert.That(component.TextField.style.unityFontDefinition.keyword, Is.EqualTo(StyleKeyword.Null));
        }
    }
}
