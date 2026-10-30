using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables
{
    public interface IWearablesProvider : IElementsProvider<ITrimmedWearable, IWearable, IWearablesProvider.Params>
    {
        public enum SortingField
        {
            Date,
            Rarity,
            Name,
        }

        public enum OrderBy
        {
            Ascending,
            Descending
        }

        [Flags]
        public enum CollectionType
        {
            Base = 1 << 0,
            OnChain = 1 << 1,
            ThirdParty = 1 << 2,
            All = -1
        }

        public struct Params
        {
            public int PageSize;
            public int PageNumber;
            public SortingField SortingField;
            public OrderBy OrderBy;
            public CollectionType CollectionType;
            public string? Category;

            /// <summary>
            ///     Several categories in one request, on top of <see cref="Category" />
            /// </summary>
            public IReadOnlyList<string>? Categories;
            public bool SmartWearablesOnly;
            public string? Name;
            public string? Network;
            public bool? IncludeAmount;

            public Params(int pageSize, int pageNumber)
            {
                PageSize = pageSize;
                PageNumber = pageNumber;
                SortingField = SortingField.Date;
                OrderBy = OrderBy.Descending;
                CollectionType = CollectionType.All;
                Category = null;
                Categories = null;
                SmartWearablesOnly = false;
                Name = null;
                Network = null;
                IncludeAmount = null;
            }
        }

        /// <summary>
        ///     Resolves the wearables' DTOs without loading their assets.
        /// </summary>
        UniTask<IReadOnlyCollection<IWearable>?> GetDTOsByPointersAsync(
            IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            CancellationToken ct,
            List<IWearable>? results = null);
    }
}
