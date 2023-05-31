using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems.Tests
{
    [TestFixture]
    public class ParcelMathShould
    {
        [Test]
        public void CheckParcelsInRange()
        {
            var centerScene = new Vector3(ParcelMathHelper.PARCEL_SIZE / 2.0f, 0.0f, -ParcelMathHelper.PARCEL_SIZE / 2.0f);
            // Test from position 0
            {
                var position = new Vector3(0.0f, 0.0f, 0.0f) + centerScene;
                var parcels = ParcelMathHelper.ParcelsInRange(position, 1);

                var expectedParcels = new List<Vector2Int>()
                {
                    new (-1,-1),
                    new (-1,0),
                    new (-1,1),
                    new (0,-1),
                    new (0,0),
                    new (0,1),
                    new (1,-1),
                    new (1,0),
                    new (1,1),
                };

                Assert.IsTrue(parcels.SequenceEqual(expectedParcels));
            }

            // Test from position 0
            {
                var position = (new Vector3(100.0f, 0.0f, -100.0f) * ParcelMathHelper.PARCEL_SIZE) + centerScene;
                var parcels = ParcelMathHelper.ParcelsInRange(position, 2);

                var expectedParcels = new List<Vector2Int>()
                {
                    new (98,99),
                    new (98,100),
                    new (98,101),
                    new (99,98),
                    new (99,99),
                    new (99,100),
                    new (99,101),
                    new (99,102),
                    new (100,98),
                    new (100,99),
                    new (100,100),
                    new (100,101),
                    new (100,102),
                    new (101,98),
                    new (101,99),
                    new (101,100),
                    new (101,101),
                    new (101,102),
                    new (102,99),
                    new (102,100),
                    new (102,101),
                };

                Assert.IsTrue(parcels.SequenceEqual(expectedParcels));
            }
        }
    }
}
