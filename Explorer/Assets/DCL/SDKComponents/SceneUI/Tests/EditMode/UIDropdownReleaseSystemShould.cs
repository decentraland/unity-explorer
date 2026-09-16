using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIDropdown;
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
    public class UIDropdownReleaseSystemShould : UnitySystemTestBase<UIDropdownReleaseSystem>
    {
        private const string FONT_SRC = "fonts/Roboto.ttf";

        private IComponentPool<UIDropdownComponent> componentPool = null!;
        private FontAsset customFont = null!;
        private UIDropdownComponent component = null!;
        private Entity entity;
        private Entity promiseEntity;

        [SetUp]
        public void SetUp()
        {
            componentPool = Substitute.For<IComponentPool<UIDropdownComponent>>();
            var poolsRegistry = new ComponentPoolsRegistry(new Dictionary<Type, IComponentPool> { { typeof(UIDropdownComponent), componentPool } }, null);
            system = new UIDropdownReleaseSystem(world, poolsRegistry);

            customFont = TestFonts.CreateUIToolkitFont();

            component = new UIDropdownComponent();
            component.Initialize("dropdown");
            component.CustomFont = customFont;
            UiElementUtils.SetFont(component.DropdownField, Font.FSansSerif, new[] { new StyleFontDefinition() }, customFont);
            var sceneData = Substitute.For<ISceneData>();
            sceneData.TryGetContentUrl(FONT_SRC, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString("https://peer.decentraland.org/content/contents/bafyfont");
                          return true;
                      });
            component.FontRequest.Update(world, sceneData, FONT_SRC, PartitionComponent.TOP_PRIORITY);
            promiseEntity = component.FontRequest.Promise!.Value.Entity;

            entity = world.Create(new PBUiDropdown { FontSrc = FONT_SRC }, component);
        }

        protected override void OnTearDown()
        {
            UnityEngine.Object.DestroyImmediate(customFont);
        }

        [Test]
        public void ReturnTheComponentToThePoolWhenItIsRemoved()
        {
            world.Remove<PBUiDropdown>(entity);

            system.Update(0);

            Assert.That(world.Has<UIDropdownComponent>(entity), Is.False);
            componentPool.Received(1).Release(component);
            AssertFontReleased();
        }

        [Test]
        public void ReturnTheComponentToThePoolWhenTheEntityIsDeleted()
        {
            world.Add(entity, new DeleteEntityIntention());

            system.Update(0);

            Assert.That(world.Has<UIDropdownComponent>(entity), Is.False);
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
            Assert.That(component.DropdownField.style.unityFontDefinition.keyword, Is.EqualTo(StyleKeyword.Null));
        }
    }
}
