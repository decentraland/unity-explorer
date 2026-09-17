using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using DCL.SDKComponents.TextShape.System;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Fonts;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.TextShape.Tests
{
    public class UpdateTextShapeSystemShould : UnitySystemTestBase<UpdateTextShapeSystem>
    {
        private const string FONT_SRC = "fonts/Roboto.ttf";

        private TMP_FontAsset builtInFont = null!;
        private FontFamilyAssets family = null!;
        private FontData fontData = null!;
        private TextMeshPro textMeshPro = null!;
        private Entity entity;

        [SetUp]
        public void SetUp()
        {
            builtInFont = TestFonts.CreateTextMeshProFont();
            family = new RuntimeFontAssetFactory(builtInFont).Create("Custom", TestFonts.PATH)!;
            textMeshPro = new GameObject(nameof(UpdateTextShapeSystemShould)).AddComponent<TextMeshPro>();
            textMeshPro.font = builtInFont;

            var sceneData = Substitute.For<ISceneData>();
            sceneData.Geometry.Returns(ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY);
            sceneData.TryGetContentUrl(FONT_SRC, out Arg.Any<URLAddress>())
                     .Returns(x =>
                      {
                          x[1] = URLAddress.FromString("https://peer.decentraland.org/content/contents/bafyfont");
                          return true;
                      });

            var fontsStorage = Substitute.For<IFontsStorage>();
            fontsStorage.Font(Arg.Any<DCL.ECSComponents.Font>()).Returns(builtInFont);
            system = new UpdateTextShapeSystem(world, fontsStorage, new MaterialPropertyBlock(),
                new EntityEventBuffer<TextShapeComponent>(1), sceneData, PartitionComponent.TOP_PRIORITY);

            var component = new TextShapeComponent(textMeshPro) { NeedsBoundsRecalculation = false };
            component.FontRequest.Update(world, sceneData, FONT_SRC, PartitionComponent.TOP_PRIORITY);
            fontData = new FontData(family);
            world.Add(component.FontRequest.Promise!.Value.Entity, new StreamableLoadingResult<FontData>(fontData));
            entity = world.Create(new PBTextShape { FontSrc = FONT_SRC, Text = "Font bounds" }, component);
            textMeshPro.transform.hasChanged = false;
        }

        protected override void OnTearDown()
        {
            Object.DestroyImmediate(textMeshPro.gameObject);
            fontData.Dispose();
            Object.DestroyImmediate(builtInFont);
        }

        [Test]
        public void DeferBoundsRecalculationUntilTheUpdateAfterTheFontIsApplied()
        {
            // Act
            system.Update(0);

            // Assert
            Assert.That(textMeshPro.font, Is.SameAs(family.TextMeshProFont));
            Assert.That(world.Get<TextShapeComponent>(entity).NeedsBoundsRecalculation, Is.True);

            // Act
            textMeshPro.ForceMeshUpdate();
            system.Update(0);

            // Assert
            Assert.That(world.Get<TextShapeComponent>(entity).NeedsBoundsRecalculation, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LeaveTheFontRequestUntouchedWhenDeletionIsPending(bool deferDeletion)
        {
            // Arrange
            PBTextShape model = world.Get<PBTextShape>(entity);
            model.FontSrc = string.Empty;
            model.IsDirty = true;
            world.Add(entity, new DeleteEntityIntention { DeferDeletion = deferDeletion });

            // Act
            system.Update(0);

            // Assert
            TextShapeComponent component = world.Get<TextShapeComponent>(entity);
            Assert.That(component.FontRequest.Src, Is.EqualTo(FONT_SRC));
            Assert.That(component.FontRequest.Promise!.Value.IsConsumed, Is.False);
            Assert.That(textMeshPro.font, Is.SameAs(builtInFont));
        }
    }
}
