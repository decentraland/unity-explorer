using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.MapRenderer.Tests.SatelliteAtlas
{
    public class SatelliteManifestShould
    {
        private const string VERSION = "2026-10-08";

        [TestCase(3, 5, 7, 5, 7)]
        [TestCase(4, 3, 2, 1, 1)]
        [TestCase(4, 15, 0, 7, 0)]
        [TestCase(8, 255, 32, 7, 1)]
        [TestCase(8, 0, 0, 0, 0)]
        public void MapATileToTheBundledChunkItSubdivides(int level, int i, int j, int expectedX, int expectedY)
        {
            // Act
            Vector2Int section = SatelliteManifest.SectionOf(new Vector3Int(i, j, level));

            // Assert
            Assert.AreEqual(new Vector2Int(expectedX, expectedY), section);
        }

        [Test]
        public void PathTilesAtTheUnversionedLayoutWithoutAManifest()
        {
            // Act
            bool published = SatelliteManifest.UNVERSIONED.TryGetTilePath(new Vector3Int(3, 2, 4), out string path);

            // Assert
            Assert.IsTrue(published);
            Assert.AreEqual("4/3%2C2.ktx2", path);
        }

        [Test]
        public void PathTilesUnderTheirSectionVersion()
        {
            // Arrange
            SatelliteManifest manifest = Manifest(("1,1", VERSION));

            // Act
            bool published = manifest.TryGetTilePath(new Vector3Int(3, 2, 4), out string path);

            // Assert
            Assert.IsTrue(published);
            Assert.AreEqual("sections/1%2C1/2026-10-08/4/3%2C2.ktx2", path);
        }

        [Test]
        public void PathEveryLevelOfASectionUnderTheSameVersion()
        {
            // Arrange
            SatelliteManifest manifest = Manifest(("7,1", VERSION));

            // Act
            manifest.TryGetTilePath(new Vector3Int(7, 1, 3), out string level3);
            manifest.TryGetTilePath(new Vector3Int(255, 32, 8), out string level8);

            // Assert
            Assert.AreEqual("sections/7%2C1/2026-10-08/3/7%2C1.ktx2", level3);
            Assert.AreEqual("sections/7%2C1/2026-10-08/8/255%2C32.ktx2", level8);
        }

        [Test]
        public void ReportATileOutsideTheListedSectionsAsUnpublished()
        {
            // Arrange
            SatelliteManifest manifest = Manifest(("1,1", VERSION));

            // Act
            bool published = manifest.TryGetTilePath(new Vector3Int(0, 0, 4), out string path);

            // Assert
            Assert.IsFalse(published);
            Assert.AreEqual(string.Empty, path);
        }

        [Test]
        public void ReportEveryTileAsUnpublishedWhenTheManifestListsNoSection()
        {
            // Arrange
            SatelliteManifest manifest = SatelliteManifest.From(new SatelliteManifest.Dto());

            // Act
            bool published = manifest.TryGetTilePath(new Vector3Int(0, 0, 4), out string _);

            // Assert
            Assert.IsFalse(published);
        }

        [Test]
        public void SkipSectionsWithAMalformedKeyOrAnEmptyVersion()
        {
            // Arrange
            SatelliteManifest manifest = Manifest(("north", VERSION), ("1", VERSION), ("1,x", VERSION), ("2,2", ""), ("3,3", null), ("0,0", VERSION));

            // Act
            bool unpublished = manifest.TryGetTilePath(new Vector3Int(2, 2, 3), out string _) || manifest.TryGetTilePath(new Vector3Int(3, 3, 3), out string _);
            bool published = manifest.TryGetTilePath(new Vector3Int(0, 0, 3), out string _);

            // Assert
            Assert.IsFalse(unpublished);
            Assert.IsTrue(published);
        }

        [Test]
        public void EscapeTheVersionInThePath()
        {
            // Arrange
            SatelliteManifest manifest = Manifest(("0,0", "2026/10 a"));

            // Act
            manifest.TryGetTilePath(new Vector3Int(0, 0, 3), out string path);

            // Assert
            Assert.AreEqual("sections/0%2C0/2026%2F10%20a/3/0%2C0.ktx2", path);
        }

        private static SatelliteManifest Manifest(params (string key, string? version)[] sections)
        {
            var dtoSections = new Dictionary<string, string?>();

            foreach ((string key, string? version) in sections)
                dtoSections[key] = version;

            return SatelliteManifest.From(new SatelliteManifest.Dto { sections = dtoSections });
        }
    }
}
