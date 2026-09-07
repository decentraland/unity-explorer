using DCL.Rendering.RenderGraphs.RenderFeatures.SkyboxToCubemap;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Tests
{
    [TestFixture]
    public class SkyboxReflectionFeatureShould
    {
        private const string REMOVED_DUPLICATE = "RendererFeature_SkyboxEnvironmentProbe";

        private static readonly string[] SHIPPED_FEATURES =
        {
            "RendererFeature_AvatarOutline",
            "RenderFeature_ObjectHighlight",
            "RendererFeature_Ocean",
            "SkyboxToCubemapRendererFeature",
        };

        [Test]
        public void BeTheOnlySkyboxReflectionFeatureInTheAssembly()
        {
            // Arrange
            var features = new List<string>();

            // Act
            foreach (Type type in typeof(SkyboxToCubemapRendererFeature).Assembly.GetTypes())
            {
                if (typeof(ScriptableRendererFeature).IsAssignableFrom(type) && !type.IsAbstract)
                    features.Add(type.Name);
            }

            // Assert
            Assert.IsFalse(features.Contains(REMOVED_DUPLICATE), $"{REMOVED_DUPLICATE} duplicates SkyboxToCubemapRendererFeature");
            CollectionAssert.AreEquivalent(SHIPPED_FEATURES, features);
        }
    }
}
