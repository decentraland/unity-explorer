using DCL.SkyBox;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Tests
{
    [TestFixture]
    public class SkyboxSettingsAssetShould
    {
        private SkyboxSettingsAsset settings = null!;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<SkyboxSettingsAsset>();

            settings.LookPresets = new[]
            {
                new SkyboxSettingsAsset.LookPresetEntry { Name = "Legacy" },
                new SkyboxSettingsAsset.LookPresetEntry { Name = "Halloween2026" },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [TestCase("Halloween2026", 1)]
        [TestCase("halloween2026", 1)]
        [TestCase("  Halloween2026 ", 1)]
        [TestCase("Legacy", 0)]
        public void FindLookPresetByNameIgnoringCaseAndWhitespace(string name, int expectedIndex)
        {
            // Act
            int index = settings.IndexOfLookPreset(name);

            // Assert
            Assert.AreEqual(expectedIndex, index);
        }

        [TestCase("Christmas2026")]
        [TestCase("")]
        public void NotFindUnknownLookPreset(string name)
        {
            // Act
            int index = settings.IndexOfLookPreset(name);

            // Assert
            Assert.AreEqual(-1, index);
        }
    }
}
