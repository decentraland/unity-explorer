using Unity.Collections;
using Unity.Mathematics;

// ReSharper disable UseMethodAny.2

namespace DCL.Landscape
{
    public struct WorldModel
    {
        public readonly bool IsEmpty;

        public readonly int2 minParcel;
        public readonly int2 maxParcel;
        public readonly int2 sizeInParcels;
        public readonly int2 centerInParcels;

        public readonly NativeParallelHashSet<int2> ownedParcels;

        public WorldModel(NativeParallelHashSet<int2> ownedParcels)
        {
            this.ownedParcels = ownedParcels;

            IsEmpty = ownedParcels.Count() == 0;

            minParcel = new int2(int.MaxValue, int.MaxValue);
            maxParcel = new int2(int.MinValue, int.MinValue);

            foreach (int2 parcel in ownedParcels)
            {
                if (parcel.x < minParcel.x) minParcel.x = parcel.x;
                else if (parcel.x > maxParcel.x) maxParcel.x = parcel.x;

                if (parcel.y < minParcel.y) minParcel.y = parcel.y;
                else if (parcel.y > maxParcel.y) maxParcel.y = parcel.y;
            }

            // +1 to include both the starting and ending points.
            sizeInParcels = new int2(maxParcel.x - minParcel.x + 1, maxParcel.y - minParcel.y + 1);
            centerInParcels = sizeInParcels / 2;
        }
    }
}
