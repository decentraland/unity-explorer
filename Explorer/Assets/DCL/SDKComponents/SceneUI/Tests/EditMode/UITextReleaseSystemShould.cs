using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIText;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;
using Font = DCL.ECSComponents.Font;
using QueryDescription = Arch.Core.QueryDescription;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UITextReleaseSystemShould : UnitySystemTestBase<UITextReleaseSystem>
    {
        private const string FONT_SRC = "fonts/Roboto.ttf";

        private IComponentPool labelPool = null!;
        private FontAsset customFont = null!;
        private Label label = null!;
        private Entity entity;
        private Entity promiseEntity;

        [SetUp]
        public void SetUp()
        {
            labelPool = Substitute.For<IComponentPool>();
            var poolsRegistry = new ComponentPoolsRegistry(new Dictionary<Type, IComponentPool> { { typeof(Label), labelPool } }, null);
            system = new UITextReleaseSystem(world, poolsRegistry);

            customFont = TestFonts.CreateUIToolkitFont();
            label = new Label();
            UiElementUtils.SetFont(label, Font.FSansSerif, new[] { new StyleFontDefinition() }, customFont);

            var component = new UITextComponent { Label = label, CustomFont = customFont };
            var sceneData = Substitute.For<ISceneData>();
            sceneData.TryGetContentUrl(FONT_SRC, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString("https://peer.decentraland.org/content/contents/bafyfont");
                          return true;
                      });
            component.FontRequest.Update(world, sceneData, FONT_SRC, PartitionComponent.TOP_PRIORITY);
            promiseEntity = component.FontRequest.Promise!.Value.Entity;

            entity = world.Create(new PBUiText { FontSrc = FONT_SRC }, component);
        }

        protected override void OnTearDown()
        {
            UnityEngine.Object.DestroyImmediate(customFont);
        }

        [Test]
        public void ReturnTheLabelToThePoolWhenTheComponentIsRemoved()
        {
            world.Remove<PBUiText>(entity);

            system.Update(0);

            Assert.That(world.Has<UITextComponent>(entity), Is.False);
            labelPool.Received(1).Release(label);
            AssertFontReleased();
        }

        [Test]
        public void ReleaseTheFontWhenTheEntityIsDeleted()
        {
            world.Add(entity, new DeleteEntityIntention());

            system.Update(0);

            Assert.That(world.Get<UITextComponent>(entity).CustomFont, Is.Null);
            labelPool.DidNotReceiveWithAnyArgs().Release(default!);
            AssertFontReleased();
        }

        [Test]
        public void KeepTheFontWhileTheDeletionIsDeferred()
        {
            world.Add(entity, new DeleteEntityIntention { DeferDeletion = true });

            system.Update(0);

            Assert.That(world.Get<UITextComponent>(entity).CustomFont, Is.SameAs(customFont));
            Assert.That(world.IsAlive(promiseEntity), Is.True);
            Assert.That(label.style.unityFontDefinition.value.fontAsset, Is.SameAs(customFont));
        }

        [Test]
        public void ReleaseEveryFontWhenTheWorldIsFinalized()
        {
            system.FinalizeComponents(world.Query(QueryDescription.Null));

            Assert.That(world.Get<UITextComponent>(entity).CustomFont, Is.Null);
            AssertFontReleased();
        }

        private void AssertFontReleased()
        {
            Assert.That(world.IsAlive(promiseEntity), Is.False);
            Assert.That(label.style.unityFontDefinition.keyword, Is.EqualTo(StyleKeyword.Null));
        }
    }
}
