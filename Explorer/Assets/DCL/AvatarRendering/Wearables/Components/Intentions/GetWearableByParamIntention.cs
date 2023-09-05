using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct GetWearableByParamIntention : IGetWearableIntention, IEquatable<GetWearableByParamIntention>
    {
        public CancellationTokenSource CancellationTokenSource { get; }
        public CommonLoadingArguments CommonArguments { get; set; }
        public bool StartAssetBundlesDownload { get; set; }

        //ValidParams: pageNum, pageSize, includeEntities (bool), rarity, category, name, orderBy, direction,
        //collectionType (base-wearable, on-chain, third-party), thirdPartyCollectionId
        public (string, string)[] Params;
        public string UserID;

        public bool Equals(GetWearableByParamIntention other) =>
            Equals(Params, other.Params) && UserID == other.UserID && Equals(CancellationTokenSource, other.CancellationTokenSource) && CommonArguments.Equals(other.CommonArguments);

        public bool Equals(IGetWearableIntention other) =>
            Equals(CancellationTokenSource, other.CancellationTokenSource) && CommonArguments.Equals(other.CommonArguments);

        public override bool Equals(object obj) =>
            obj is GetWearableByParamIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Params, UserID, CancellationTokenSource, CommonArguments);

    }
}
