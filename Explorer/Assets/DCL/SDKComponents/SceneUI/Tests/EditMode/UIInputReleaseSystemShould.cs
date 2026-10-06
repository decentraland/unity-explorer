using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIInput;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Fonts;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using TMPro;
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
        private const string FONT_SRC = "fonts/Roboto.ttf";

        private IComponentPool componentPool = null!;
        private TMP_FontAsset referenceFont = null!;
        private FontData fontData = null!;
        private FontAsset customFont = null!;
        private UIInputComponent component = null!;
        private Entity entity;

        [SetUp]
        public void SetUp()
        {
            componentPool = Substitute.For<IComponentPool>();
            var poolsRegistry = new ComponentPoolsRegistry(new Dictionary<Type, IComponentPool> { { typeof(UIInputComponent), componentPool } }, null);
            system = new UIInputReleaseSystem(world, poolsRegistry);

            referenceFont = TestFonts.CreateTextMeshProFont();
            fontData = TestFonts.CreateBundledFont(referenceFont);
            customFont = fontData.Asset.UIToolkitFont;

            component = new UIInputComponent();
            component.Initialize(Substitute.For<IInputBlock>(), "input", string.Empty, string.Empty, Color.white);
            UiElementUtils.SetFont(component.TextField, Font.FSansSerif, new[] { new StyleFontDefinition() }, customFont);
            var sceneData = Substitute.For<ISceneData>();
            sceneData.TryGetContentUrl(FONT_SRC, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString("https://peer.decentraland.org/content/contents/bafyfont");
                          return true;
                      });
            component.FontRequest.Update(world, sceneData, FONT_SRC, PartitionComponent.TOP_PRIORITY);
            Entity promiseEntity = component.FontRequest.Promise!.Value.Entity;
            ((IStreamableRefCountData)fontData).AddReference();
            world.Add(promiseEntity, new StreamableLoadingResult<FontData>(fontData));
            component.FontRequest.TryConsume(world);

            entity = world.Create(new PBUiInput { FontSrc = FONT_SRC }, component);
        }

        protected override void OnTearDown()
        {
            TestFonts.DestroyBundledFont(fontData);
            UnityEngine.Object.DestroyImmediate(referenceFont);
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
            Assert.That(component.FontRequest.Assets, Is.Null);
            Assert.That(component.FontRequest.Promise, Is.Null);
            Assert.That(fontData.CanBeDisposed(), Is.True);
            Assert.That(component.TextField.style.unityFontDefinition.keyword, Is.EqualTo(StyleKeyword.Null));
        }
    }
}
