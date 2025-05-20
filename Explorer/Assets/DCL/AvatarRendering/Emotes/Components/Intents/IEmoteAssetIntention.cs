using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Emotes
{
    public interface IEmoteAssetIntention : IAssetIntention
    {
        BodyShape BodyShape { get; }
        LoadTimeout Timeout { get; }
        bool Loop { get; }
        URN NewSceneEmoteURN();
        void CreateAndAddPromiseToWorld(World world, IPartitionComponent partitionComponent, URLSubdirectory? customStreamingSubdirectory, IEmote emote);
    }
}
