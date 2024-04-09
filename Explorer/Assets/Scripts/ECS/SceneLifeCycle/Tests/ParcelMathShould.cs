using NUnit.Framework;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Tests
{

    public class ParcelMathShould
    {

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
    }
}
