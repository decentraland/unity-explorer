using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Components;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class StartLoadingTextureSystem : StartLoadingSystemBase<GetTextureIntention, Texture2D>
    {
        internal StartLoadingTextureSystem(World world) : base(world) { }

        protected override UnityWebRequest CreateWebRequest(in GetTextureIntention intention) =>
            UnityWebRequestTexture.GetTexture(intention.CommonArguments.URL, !intention.IsReadable);
    }
}
