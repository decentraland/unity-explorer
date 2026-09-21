using NUnit.Framework;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace DCL.SceneLifeCycle.Tests
{
    [TestFixture]
    public class ParcelMathShould
    {
        [Test]
        public void CheckParcelsInRange()
        {
            var parcels = new HashSet<int2>(100);
            var centerScene = new Vector3(ParcelMathHelper.PARCEL_SIZE / 2.0f, 0.0f, -ParcelMathHelper.PARCEL_SIZE / 2.0f);

            // Test from position 0
            {
                Vector3 position = new Vector3(0.0f, 0.0f, 0.0f) + centerScene;
                ParcelMathHelper.ParcelsInRange(position, 1, parcels);

                var expectedParcels = new List<int2>
                {
                    new (-1, -2),
                    new (-1, -1),
                    new (-1, 0),
                    new (0, -2),
                    new (0, -1),
                    new (0, 0),
                    new (1, -2),
                    new (1, -1),
                    new (1, 0),
                };

                CollectionAssert.AreEquivalent(expectedParcels, parcels);
            }

            // Test from position 0
            {
                Vector3 position = (new Vector3(100.0f, 0.0f, -100.0f) * ParcelMathHelper.PARCEL_SIZE) + centerScene;
                ParcelMathHelper.ParcelsInRange(position, 2, parcels);

                var expectedParcels = new List<int2>
                {
                    new (98, -102),
                    new (98, -101),
                    new (98, -100),
                    new (99, -103),
                    new (99, -102),
                    new (99, -101),
                    new (99, -100),
                    new (99, -99),
                    new (100, -103),
                    new (100, -102),
                    new (100, -101),
                    new (100, -100),
                    new (100, -99),
                    new (101, -103),
                    new (101, -102),
                    new (101, -101),
                    new (101, -100),
                    new (101, -99),
                    new (102, -102),
                    new (102, -101),
                    new (102, -100),
                };

                CollectionAssert.AreEquivalent(expectedParcels, parcels);
            }
        }

        [Test]
        public void LimitSceneHeightByParcels()
        {
            List<ParcelMathHelper.ParcelCorners> corners = CreateCorners(Vector2Int.zero, Vector2Int.right, Vector2Int.up);

            ParcelMathHelper.SceneGeometry geometry = ParcelMathHelper.CreateSceneGeometry(corners, Vector2Int.zero, limitHeightByParcels: true);

            // log2(3 + 1) x 20
            Assert.That(geometry.Height, Is.EqualTo(40f).Within(0.001f));
        }

        [Test]
        public void UseFixedSceneHeightWhenNotLimitedByParcels()
        {
            List<ParcelMathHelper.ParcelCorners> corners = CreateCorners(Vector2Int.zero, Vector2Int.right, Vector2Int.up);

            ParcelMathHelper.SceneGeometry geometry = ParcelMathHelper.CreateSceneGeometry(corners, Vector2Int.zero, limitHeightByParcels: false);

            Assert.That(geometry.Height, Is.EqualTo(ParcelMathHelper.FIXED_SCENE_HEIGHT));
        }

        private static List<ParcelMathHelper.ParcelCorners> CreateCorners(params Vector2Int[] parcels)
        {
            var corners = new List<ParcelMathHelper.ParcelCorners>(parcels.Length);

            foreach (Vector2Int parcel in parcels)
                corners.Add(ParcelMathHelper.CalculateCorners(parcel));

            return corners;
        }
    }
}
