using ECS.Prioritization.Components;
using System.Collections.Generic;
using Utility;

namespace ECS.Prioritization
{
    public class DistanceBasedComparer : IComparer<IPartitionComponent>
    {
        public static readonly DistanceBasedComparer INSTANCE = new ();

        public int Compare(IPartitionComponent x, IPartitionComponent y)
        {
            // discrete distance comparison
            // break down by SQR_PARCEL_SIZE

            float xParcelBucket = x.RawSqrDistance / ParcelMathHelper.SQR_PARCEL_SIZE;
            float yParcelBucket = y.RawSqrDistance / ParcelMathHelper.SQR_PARCEL_SIZE;

            int bucketComparison = xParcelBucket.CompareTo(yParcelBucket);
            return bucketComparison != 0 ? bucketComparison : x.IsBehind.CompareTo(y.IsBehind);
        }
    }
}
