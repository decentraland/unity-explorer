using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables
{
    public interface IWearablesProvider
    {
        UniTask<(IReadOnlyList<IWearable> results, int totalAmount)> GetAsync(
            int pageSize,
            int pageNumber,
            CancellationToken ct,
            SortingField sortingField = SortingField.Date,
            OrderBy orderBy = OrderBy.Descending,
            string? category = null,
            CollectionType collectionType = CollectionType.All, // Default for Backpack
            string? name = null,
            List<IWearable>? results = null,
            string? network = null,
            bool? includeAmount = null,
            CommonLoadingArguments? loadingArguments = null,
            bool needsBuilderAPISigning = false
        );
        
        UniTask<IReadOnlyCollection<IWearable>?> RequestPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            CancellationToken ct);

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
            All = Base | OnChain | ThirdParty,
            None = 0
        }
    }
}
