using ECS.Prioritization.Components;
using System.Collections.Generic;
using Utility;

namespace ECS.Prioritization
{
    public class DistanceBasedComparer : IComparer<IPartitionComponent>
    {
        public static readonly DistanceBasedComparer INSTANCE = new ();

        public int Compare(IPartitionComponent x, IPartitionComponent y) =>
            Compare(new DataSurrogate(x.RawSqrDistance, x.IsBehind), new DataSurrogate(y.RawSqrDistance, y.IsBehind));

        public static int Compare(DataSurrogate x, DataSurrogate y)
        {
            // discrete distance comparison
            // break down by SQR_PARCEL_SIZE

            float xParcelBucket = x.RawSqrDistance / ParcelMathHelper.SQR_PARCEL_SIZE;
            float yParcelBucket = y.RawSqrDistance / ParcelMathHelper.SQR_PARCEL_SIZE;

            int bucketComparison = xParcelBucket.CompareTo(yParcelBucket);
            return bucketComparison != 0 ? bucketComparison : x.IsBehind.CompareTo(y.IsBehind);
        }

        /// <summary>
        ///     Blittable data to be used in the comparer
        /// </summary>
        public readonly struct DataSurrogate
        {
            public readonly bool IsBehind;
            public readonly float RawSqrDistance;

            public DataSurrogate(float rawSqrDistance, bool isBehind)
            {
                RawSqrDistance = rawSqrDistance;
                IsBehind = isBehind;
            }
        }
    }
}
