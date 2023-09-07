using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct GetWearableDTOByParamIntention : IEquatable<GetWearableDTOByParamIntention>, ILoadingIntention
    {
        public CancellationTokenSource CancellationTokenSource { get; }
        public CommonLoadingArguments CommonArguments { get; set; }

        //ValidParams: pageNum, pageSize, includeEntities (bool), rarity, category, name, orderBy, direction,
        //collectionType (base-wearable, on-chain, third-party), thirdPartyCollectionId
        public (string, string)[] Params;
        public string UserID;

        public bool Equals(GetWearableDTOByParamIntention other) =>
            Equals(Params, other.Params) && UserID == other.UserID && Equals(CancellationTokenSource, other.CancellationTokenSource) && CommonArguments.Equals(other.CommonArguments);

        public override bool Equals(object obj) =>
            obj is GetWearableDTOByParamIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Params, UserID, CancellationTokenSource, CommonArguments);

    }
}
