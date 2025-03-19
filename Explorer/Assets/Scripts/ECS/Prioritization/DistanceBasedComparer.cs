using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using Utility;

namespace ECS.Prioritization
{
    public class DistanceBasedComparer : IComparer<IPartitionComponent>
    {
        public static readonly DistanceBasedComparer INSTANCE = new ();

        public int Compare(IPartitionComponent x, IPartitionComponent y) =>
            Compare(new DataSurrogate(x.RawSqrDistance, x.IsBehind, false, 0), new DataSurrogate(y.RawSqrDistance, y.IsBehind, false, 0));

        public static int Compare(DataSurrogate x, DataSurrogate y)
        {
            if (x.IsPlayerInsideParcel && !y.IsPlayerInsideParcel) return -1;
            if (y.IsPlayerInsideParcel && !x.IsPlayerInsideParcel) return 1;

            // discrete distance comparison
            // break down by SQR_PARCEL_SIZE

            float xParcelBucket = x.RawSqrDistance / ParcelMathHelper.SQR_PARCEL_SIZE;
            float yParcelBucket = y.RawSqrDistance / ParcelMathHelper.SQR_PARCEL_SIZE;

            int bucketComparison = xParcelBucket.CompareTo(yParcelBucket);

            if (bucketComparison != 0)
                return bucketComparison;

            int compareIsBehind = x.IsBehind.CompareTo(y.IsBehind);

            if (compareIsBehind != 0)
                return compareIsBehind;

            //If everything fails, the scene on the right has higher priority
            return x.XCoordinate.CompareTo(y.XCoordinate);
        }

        /// <summary>
        ///     Blittable data to be used in the comparer
        /// </summary>
        public readonly struct DataSurrogate
        {
            public readonly bool IsBehind;
            public readonly float RawSqrDistance;
            public readonly bool IsPlayerInsideParcel;
            public readonly int XCoordinate;

            public DataSurrogate(float rawSqrDistance, bool isBehind, bool isPlayerInsideParcel, int xCoordinate)
            {
                RawSqrDistance = rawSqrDistance;
                IsBehind = isBehind;
                IsPlayerInsideParcel = isPlayerInsideParcel;
                XCoordinate = xCoordinate;
            }
        }
    }
}
