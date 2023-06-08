using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Components;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class StartLoadingTextureSystem : StartLoadingSystemBase<GetTextureIntention>
    {
        internal StartLoadingTextureSystem(World world) : base(world) { }

        protected override UnityWebRequest CreateWebRequest(in GetTextureIntention intention) =>
            GetTextureRequest(intention);

        internal static UnityWebRequest GetTextureRequest(in GetTextureIntention intention) =>
            UnityWebRequestTexture.GetTexture(intention.CommonArguments.URL, !intention.IsReadable);
    }
}
