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
        bool Loop { get; }
        string EmoteHash { get; }
        URN NewSceneEmoteURN();
        /// <param name="useLocalAssetBundles">Whether a local scene emote may load from the local asset-bundle server; realm emotes always load bundles.</param>
        void CreateAndAddPromiseToWorld(World world, IPartitionComponent partitionComponent, URLSubdirectory? customStreamingSubdirectory, bool useLocalAssetBundles, IEmote emote);
        bool IsTimeout(float deltaTime);
    }
}
