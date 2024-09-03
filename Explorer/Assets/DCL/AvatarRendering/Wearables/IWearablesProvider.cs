using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables
{
    public interface IWearablesProvider
    {
        UniTask<(IReadOnlyList<IWearable> results, int totalAmount)> GetAsync(int pageSize, int pageNumber, CancellationToken ct,
            SortingField sortingField = SortingField.Date, OrderBy orderBy = OrderBy.Descending,
            string? category = null, CollectionType collectionType = CollectionType.All,
            string? name = null,
            List<IWearable>? results = null);

        public enum SortingField
        {
            Date,
            Rarity,
            Name,
        }

        enum OrderBy
        {
            Ascending,
            Descending
        }

        [Flags]
        enum CollectionType
        {
            Base = 1 << 0,
            OnChain = 1 << 1,
            ThirdParty = 1 << 2,
            All = -1,
        }
    }
}
