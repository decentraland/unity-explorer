using DCL.AvatarRendering.Wearables;
using System;

namespace DCL.Backpack
{
    public struct BackpackGridSort
    {
        public NftOrderByOperation OrderByOperation;
        public bool SortAscending;

        public BackpackGridSort(NftOrderByOperation orderByOperation, bool sortAscending)
        {
            OrderByOperation = orderByOperation;
            SortAscending = sortAscending;
        }
    }

    public enum NftOrderByOperation
    {
        Date,
        Rarity,
        Name,
    }

    public static class NftOrderByOperationExtensions
    {
        public static IWearablesProvider.SortingField ToSortingField(this NftOrderByOperation nftOrderByOperation)
        {
            return nftOrderByOperation switch
                   {
                       NftOrderByOperation.Date => IWearablesProvider.SortingField.Date,
                       NftOrderByOperation.Name => IWearablesProvider.SortingField.Name,
                       NftOrderByOperation.Rarity => IWearablesProvider.SortingField.Rarity,
                       _ => throw new ArgumentOutOfRangeException(nameof(nftOrderByOperation), nftOrderByOperation, null)
                   };
        }
    }
}
