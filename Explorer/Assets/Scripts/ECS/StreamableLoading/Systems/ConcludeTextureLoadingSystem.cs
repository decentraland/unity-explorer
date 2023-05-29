using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Components;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(StartLoadingTextureSystem))]
    public partial class ConcludeTextureLoadingSystem : ConcludeLoadingSystemBase<Texture2D, GetTextureIntention>
    {
        internal ConcludeTextureLoadingSystem(World world) : base(world) { }

        protected override Texture2D GetAsset(UnityWebRequest webRequest, in GetTextureIntention getTextureIntention)
        {
            Texture2D tex = DownloadHandlerTexture.GetContent(webRequest);
            tex.wrapMode = getTextureIntention.WrapMode;
            tex.filterMode = getTextureIntention.FilterMode;
            return tex;
        }
    }
}
