using NUnit.Framework;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;

namespace Utility.Tests
{
    public class ParcelMathJobifiedHelperShould
    {
        private static readonly int2[] EXPECTED_CELLS_N1 =
        {
            new (1, 1), new (1, 0), new (1, -1),
            new (0, -1), new (-1, -1),
            new (-1, -0), new (-1, 1), new (0, 1),
        };

        private static readonly int2[] EXPECTED_CELLS_N2 =
        {
            new (2, 2), new (2, 1), new (2, 0), new (2, -1), new (2, -2),
            new (1, -2), new (0, -2), new (-1, -2), new (-2, -2),
            new (-2, -1), new (-2, 0), new (-2, 1), new (-2, 2),
            new (-1, 2), new (0, 2), new (1, 2),
        };

        private static readonly int2[] EXPECTED_CELLS_N3 =
        {
            new (3, 3), new (3, 2), new (3, 1), new (3, 0), new (3, -1), new (3, -2), new (3, -3),
            new (2, -3), new (1, -3), new (0, -3), new (-1, -3), new (-2, -3), new (-3, -3),
            new (-3, -2), new (-3, -1), new (-3, 0), new (-3, 1), new (-3, 2), new (-3, 3),
            new (-2, 3), new (-1, 3), new (0, 3), new (1, 3), new (2, 3),
        };

        private static readonly int2[] EXPECTED_CELLS_N4 =
        {
            new (4, 4), new (4, 3), new (4, 2), new (4, 1), new (4, 0), new (4, -1), new (4, -2), new (4, -3), new (4, -4),
            new (3, -4), new (2, -4), new (1, -4), new (0, -4), new (-1, -4), new (-2, -4), new (-3, -4), new (-4, -4),
            new (-4, -3), new (-4, -2), new (-4, -1), new (-4, 0), new (-4, 1), new (-4, 2), new (-4, 3), new (-4, 4),
            new (-3, 4), new (-2, 4), new (-1, 4), new (0, 4), new (1, 4), new (2, 4), new (3, 4),
        };

        [Test]
        public void CalculateRingRadius4()
        {
            var helper = new ParcelMathJobifiedHelper();

            using var processedParcels = new NativeHashSet<int2>(0, AllocatorManager.Persistent);

            helper.StartParcelsRingSplit(int2.zero, 4, processedParcels);
            ref readonly NativeArray<ParcelMathJobifiedHelper.ParcelInfo> result = ref helper.FinishParcelsRingSplit();

            Assert.That(helper.GetRing(0).Select(p => p.Parcel), Is.EquivalentTo(new[]
            {
                new int2(0, 0),
            }));

            Assert.That(helper.GetRing(1).Select(p => p.Parcel), Is.EquivalentTo(EXPECTED_CELLS_N1));
            Assert.That(helper.GetRing(2).Select(p => p.Parcel), Is.EquivalentTo(EXPECTED_CELLS_N2));
            Assert.That(helper.GetRing(3).Select(p => p.Parcel), Is.EquivalentTo(EXPECTED_CELLS_N3));
            Assert.That(helper.GetRing(4).Select(p => p.Parcel), Is.EquivalentTo(EXPECTED_CELLS_N4));
        }
    }
}
