using ECS.StreamableLoading.Common.Components;
using System;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface IGetWearableIntention : ILoadingIntention, IEquatable<IGetWearableIntention>
    {
        bool StartAssetBundlesDownload { get; set; }
    }
}
