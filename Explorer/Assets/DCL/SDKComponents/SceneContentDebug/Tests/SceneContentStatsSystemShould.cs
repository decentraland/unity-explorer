using Arch.Core;
using CRDT;
using DCL.Profiling;
using DCL.SDKComponents.SceneContentDebug.Systems;
using DCL.SDKComponents.SceneUI.Components;
using DCL.WebRequests;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.SceneContentDebug.Tests
{
    public class SceneContentStatsSystemShould : UnitySystemTestBase<SceneContentStatsSystem>
    {
        private SceneRuntimeMetrics runtimeMetrics = null!;
        private Texture2D texture = null!;

        [SetUp]
        public void Setup()
        {
            runtimeMetrics = new SceneRuntimeMetrics();
            texture = new Texture2D(2, 2);

            system = new SceneContentStatsSystem(world, runtimeMetrics, new Dictionary<CRDTEntity, Entity>());

            // A consumer must hold the demand flag, otherwise the system never collects.
            runtimeMetrics.ContentStats.McpRequests = 1;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void CountAUiBackgroundTextureOnceAcrossElements()
        {
            // Arrange — two backgrounds share one loaded texture
            world.Create(CreateBackground(texture));
            world.Create(CreateBackground(texture));

            // Act
            system.Update(0);

            // Assert
            SceneContentStats stats = runtimeMetrics.ContentStats;
            Assert.That(stats.HasData, Is.True);
            Assert.That(stats.UiTextures, Is.EqualTo(1));
            Assert.That(stats.Textures, Is.EqualTo(1));
        }

        [Test]
        public void IgnoreAUiBackgroundWhoseTextureIsNotLoaded()
        {
            // Arrange
            world.Create(new UIBackgroundComponent());
            world.Create(new UIBackgroundComponent { TexturePromise = TexturePromise.CreateFinalized(CreateIntention(), null) });

            // Act
            system.Update(0);

            // Assert
            Assert.That(runtimeMetrics.ContentStats.UiTextures, Is.EqualTo(0));
            Assert.That(runtimeMetrics.ContentStats.Textures, Is.EqualTo(0));
        }

        private static UIBackgroundComponent CreateBackground(Texture2D loadedTexture) =>
            new ()
            {
                TexturePromise = TexturePromise.CreateFinalized(CreateIntention(), new StreamableLoadingResult<TextureData>(new TextureData(loadedTexture))),
            };

        private static GetTextureIntention CreateIntention() =>
            new ("test-url", "test-hash", TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, "test");
    }
}
