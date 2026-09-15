using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using DCL.SDKComponents.TextShape.System;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using TMPro;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Tests
{
    public class ReleaseTextShapeSystemShould : UnitySystemTestBase<ReleaseTextShapeSystem>
    {
        private const string FONT_SRC = "Roboto";

        private IComponentPool<TextMeshPro> textMeshProPool = null!;
        private TMP_FontAsset builtInFont = null!;
        private TMP_FontAsset customFont = null!;
        private TextMeshPro textMeshPro = null!;
        private Entity entity;
        private Entity promiseEntity;

        [SetUp]
        public void SetUp()
        {
            textMeshProPool = Substitute.For<IComponentPool<TextMeshPro>>();
            builtInFont = TestFonts.CreateTextMeshProFont();
            customFont = TestFonts.CreateTextMeshProFont();
            textMeshPro = new GameObject(nameof(ReleaseTextShapeSystemShould)).AddComponent<TextMeshPro>();
            textMeshPro.font = customFont;

            system = new ReleaseTextShapeSystem(world, new IFontsStorage.Fake(builtInFont), textMeshProPool);

            var component = new TextShapeComponent(textMeshPro) { CustomFont = customFont };
            component.FontRequest.Update(world, Substitute.For<ISceneData>(), FONT_SRC, PartitionComponent.TOP_PRIORITY);
            promiseEntity = component.FontRequest.Promise!.Value.Entity;

            entity = world.Create(new PBTextShape { FontSrc = FONT_SRC }, component);
        }

        protected override void OnTearDown()
        {
            Object.DestroyImmediate(textMeshPro.gameObject);
            Object.DestroyImmediate(customFont);
            Object.DestroyImmediate(builtInFont);
        }

        [Test]
        public void ReturnTheTextToThePoolWhenTheComponentIsRemoved()
        {
            world.Remove<PBTextShape>(entity);

            system.Update(0);

            Assert.That(world.Has<TextShapeComponent>(entity), Is.False);
            textMeshProPool.Received(1).Release(textMeshPro);
            AssertFontReleased();
        }

        [Test]
        public void ReleaseTheFontWhenTheEntityIsDeleted()
        {
            world.Add(entity, new DeleteEntityIntention());

            system.Update(0);

            AssertComponentReleased();
            textMeshProPool.DidNotReceive().Release(textMeshPro);
        }

        [Test]
        public void KeepTheFontWhileTheDeletionIsDeferred()
        {
            world.Add(entity, new DeleteEntityIntention { DeferDeletion = true });

            system.Update(0);

            ref TextShapeComponent component = ref world.Get<TextShapeComponent>(entity);
            Assert.That(component.CustomFont, Is.SameAs(customFont));
            Assert.That(component.FontRequest.Src, Is.EqualTo(FONT_SRC));
            Assert.That(world.IsAlive(promiseEntity), Is.True);
            Assert.That(textMeshPro.font, Is.SameAs(customFont));
        }

        [Test]
        public void ReleaseEveryFontWhenTheWorldIsFinalized()
        {
            system.FinalizeComponents(world.Query(QueryDescription.Null));

            AssertComponentReleased();
        }

        private void AssertComponentReleased()
        {
            ref TextShapeComponent component = ref world.Get<TextShapeComponent>(entity);
            Assert.That(component.CustomFont, Is.Null);
            Assert.That(component.FontRequest.Src, Is.Null);
            Assert.That(component.FontRequest.Promise, Is.Null);
            AssertFontReleased();
        }

        private void AssertFontReleased()
        {
            Assert.That(world.IsAlive(promiseEntity), Is.False);
            Assert.That(textMeshPro.font, Is.SameAs(builtInFont));
        }
    }
}
