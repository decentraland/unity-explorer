using DCL.AvatarRendering.Wearables.Components.Intentions;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct GetWearableDTOByParamIntention : IEquatable<GetWearableDTOByParamIntention>, ILoadingIntention
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public CommonLoadingArguments CommonArguments { get; set; }

        //ValidParams: pageNum, pageSize, includeEntities (bool), rarity, category, name, orderBy, direction,
        //collectionType (base-wearable, on-chain, third-party), thirdPartyCollectionId
        public (string, string)[] Params;
        public string UserID;

        public GetWearableDTOByParamIntention((string, string)[] requestParams, string userID)
        {
            Params = requestParams;
            UserID = userID;
            CommonArguments = new CommonLoadingArguments(string.Empty);
        }

        public bool Equals(GetWearableDTOByParamIntention other) =>
            Equals(Params, other.Params) && UserID == other.UserID;

        public override bool Equals(object obj) =>
            obj is GetWearableDTOByParamIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Params, UserID, CancellationTokenSource, CommonArguments);

        public static GetWearableDTOByParamIntention FromParamIntention(GetWearableByParamIntention getWearableByParamIntention) =>
            new (getWearableByParamIntention.Params, getWearableByParamIntention.UserID);

    }
}
