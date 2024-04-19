using Unity.Collections;
using Unity.Mathematics;

// ReSharper disable UseMethodAny.2
namespace DCL.Landscape
{
    public struct WorldModel
    {
        public readonly int2 SizeInParcels;
        public readonly int2 CenterInParcels;

        public readonly NativeParallelHashSet<int2> Parcels;
        public readonly NativeParallelHashSet<int2> OwnedParcels;

        public WorldModel(NativeParallelHashSet<int2> parcels)
        {
            this.Parcels = parcels;
            OwnedParcels = parcels;

            (int2 minParcel, int2 maxParcel) = CalculateMinMaxParcels(parcels);

            // +1 to include both the starting and ending points.
            SizeInParcels = new int2(maxParcel.x - minParcel.x + 1, maxParcel.y - minParcel.y + 1);
            CenterInParcels = minParcel + (SizeInParcels / 2);
        }

        public WorldModel(NativeParallelHashSet<int2> ownedParcels, NativeArray<int2> emptyParcels)
        {
            this.Parcels = CombineArrays(emptyParcels, ownedParcels);
            OwnedParcels = ownedParcels;

            (int2 minParcel, int2 maxParcel) = CalculateMinMaxParcels(Parcels);

            // +1 to include both the starting and ending points.
            SizeInParcels = new int2(maxParcel.x - minParcel.x + 1, maxParcel.y - minParcel.y + 1);
            CenterInParcels = minParcel + (SizeInParcels / 2);
        }

        private static (int2 min,int2 max) CalculateMinMaxParcels(NativeParallelHashSet<int2> ownedParcels)
        {
            var minParcel = new int2(int.MaxValue, int.MaxValue);
            var maxParcel = new int2(int.MinValue, int.MinValue);

            foreach (int2 parcel in ownedParcels)
            {
                if (parcel.x < minParcel.x) minParcel.x = parcel.x;
                else if (parcel.x > maxParcel.x) maxParcel.x = parcel.x;

                if (parcel.y < minParcel.y) minParcel.y = parcel.y;
                else if (parcel.y > maxParcel.y) maxParcel.y = parcel.y;
            }

            return (minParcel, maxParcel);
        }

        private static NativeParallelHashSet<int2> CombineArrays(NativeArray<int2> array, NativeParallelHashSet<int2> hashSet)
        {
            NativeParallelHashSet<int2> combinedSet = new NativeParallelHashSet<int2>(array.Length + hashSet.Count(), Allocator.Temp);

            foreach (var el in array)
                combinedSet.Add(el);

            NativeParallelHashSet<int2>.Enumerator enumerator = hashSet.GetEnumerator();
            while (enumerator.MoveNext())
                combinedSet.Add(enumerator.Current);

            enumerator.Dispose();

            return combinedSet;
        }
    }
}
