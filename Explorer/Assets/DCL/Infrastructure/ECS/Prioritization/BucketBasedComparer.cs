using ECS.Prioritization.Components;
using System.Collections.Generic;

namespace ECS.Prioritization
{
    public class BucketBasedComparer : IComparer<IPartitionComponent>
    {
        public static readonly BucketBasedComparer INSTANCE = new ();

        public int Compare(IPartitionComponent x, IPartitionComponent y)
        {
            // First compare by bucket so the ordering will look like this:
            // [0 Front; 0 Behind; 1 Front; 1 Behind; ..]
            int bucketComparison = x.Bucket.CompareTo(y.Bucket);
            return bucketComparison != 0 ? bucketComparison : x.IsBehind.CompareTo(y.IsBehind);
        }
    }
}
