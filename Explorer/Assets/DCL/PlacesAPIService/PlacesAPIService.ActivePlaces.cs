using Cysharp.Threading.Tasks;
using DCL.Lambdas;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PlacesAPIService
{
    public partial class PlacesAPIService : ILambdaServiceConsumer<PlacesData.PlacesAPIResponse, PlacesAPIService.ActivePlaceKey>
    {
        internal readonly Dictionary<ActivePlaceKey, LambdaResponsePagePointer<PlacesData.PlacesAPIResponse, ActivePlaceKey>> activePlacesPagePointers = new ();

        public async UniTask<(IReadOnlyList<PlacesData.PlaceInfo> places, int total)> GetMostActivePlacesAsync(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default,
            bool renewCache = false)
        {
            var createNewPointer = false;
            var key = new ActivePlaceKey(pageSize, filter, sort);

            if (!activePlacesPagePointers.TryGetValue(key, out LambdaResponsePagePointer<PlacesData.PlacesAPIResponse, ActivePlaceKey> pagePointer))
                createNewPointer = true;
            else if (renewCache)
            {
                pagePointer.Dispose();
                activePlacesPagePointers.Remove(key);
                createNewPointer = true;
            }

            if (createNewPointer)
            {
                activePlacesPagePointers[key] = pagePointer = new LambdaResponsePagePointer<PlacesData.PlacesAPIResponse, ActivePlaceKey>(
                    "", // not needed, the consumer will compose the URL
                    pageSize, disposeCts.Token, this, TimeSpan.FromSeconds(30));
            }

            (PlacesData.PlacesAPIResponse response, bool _) = await pagePointer.GetPageAsync(pageNumber, key, ct);

            foreach (PlacesData.PlaceInfo place in response.data)
                TryCachePlace(place);

            return (response.data, response.total);
        }

        async UniTask<(PlacesData.PlacesAPIResponse response, bool success)> ILambdaServiceConsumer<PlacesData.PlacesAPIResponse, ActivePlaceKey>.CreateRequestAsync(string endPoint, int pageSize, int pageNumber, ActivePlaceKey additionalData, CancellationToken ct = default)
        {
            PlacesData.PlacesAPIResponse response = await client.GetMostActivePlacesAsync(pageNumber, pageSize, additionalData.Filter, additionalData.Sort, ct);

            // Client will handle most of the error handling and throw if needed
            return (response, true);
        }

        internal readonly struct ActivePlaceKey : IEquatable<ActivePlaceKey>
        {
            public readonly string Filter;
            public readonly int PageSize;
            public readonly string Sort;

            public ActivePlaceKey(int pageSize, string filter, string sort)
            {
                PageSize = pageSize;
                Filter = filter;
                Sort = sort;
            }

            public bool Equals(ActivePlaceKey other) =>
                PageSize == other.PageSize && Filter == other.Filter && Sort == other.Sort;

            public override bool Equals(object obj) =>
                obj is ActivePlaceKey other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(PageSize, Filter, Sort);
        }
    }
}
