using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetWearableByParamIntention : IEquatable<GetWearableByParamIntention>, ILoadingIntention
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }

        //ValidParams: pageNum, pageSize, includeEntities (bool), rarity, category, name, orderBy, direction,
        //collectionType (base-wearable, on-chain, third-party), thirdPartyCollectionId
        public IReadOnlyList<(string, string)> Params;
        public string UserID;

        //Used for pooling
        public List<IWearable> Results;
        public int TotalAmount;

        public GetWearableByParamIntention(IReadOnlyList<(string, string)> requestParams, string userID, List<IWearable> results, int totalAmount)
        {
            Params = requestParams;
            UserID = userID;
            Results = results;
            TotalAmount = totalAmount;
            CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY, cancellationTokenSource: new CancellationTokenSource());
        }

        public bool Equals(GetWearableByParamIntention other) =>
            Equals(Params, other.Params) && UserID == other.UserID;

        public override bool Equals(object obj) =>
            obj is GetWearableByParamIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Params, UserID, CancellationTokenSource, CommonArguments);
    }
}
