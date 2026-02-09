using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetTrimmedWearableByParamIntention : IEquatable<GetTrimmedWearableByParamIntention>, IAttachmentsLoadingIntention<ITrimmedWearable>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }

        //ValidParams: pageNum, pageSize, includeEntities (bool), rarity, category, name, orderBy, direction,
        //collectionType (base-wearable, on-chain, third-party), thirdPartyCollectionId
        public IReadOnlyList<(string, string)> Params { get; }
        public string UserID { get; }

        //Used for pooling
        public List<ITrimmedWearable> Results;
        public int TotalAmount { get; private set; }

        public void SetTotal(int total)
        {
            TotalAmount = total;
        }

        public void AppendToResult(ITrimmedWearable resultElement)
        {
            Results.Add(resultElement);
        }

        public bool NeedsBuilderAPISigning { get; }


        public GetTrimmedWearableByParamIntention(IReadOnlyList<(string, string)> requestParams, string userID, List<ITrimmedWearable> results, int totalAmount, bool needsBuilderAPISigning = false)
        {
            Params = requestParams;
            UserID = userID;
            Results = results;
            TotalAmount = totalAmount;
            NeedsBuilderAPISigning = needsBuilderAPISigning;
            CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY, cancellationTokenSource: new CancellationTokenSource());
        }

        public bool Equals(GetTrimmedWearableByParamIntention other) =>
            Equals(Params, other.Params) && UserID == other.UserID;

        public override bool Equals(object obj) =>
            obj is GetTrimmedWearableByParamIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Params, UserID);
    }
}
