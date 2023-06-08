using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Systems;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(StartLoadingTextureSystem))]
    public partial class ConcludeTextureLoadingSystem : ConcludeLoadingSystemBase<Texture2D, GetTextureIntention>
    {
        internal ConcludeTextureLoadingSystem(World world, IStreamableCache<Texture2D, GetTextureIntention> cache) : base(world, cache) { }

        protected override Texture2D GetAsset(UnityWebRequest webRequest, in GetTextureIntention getTextureIntention)
        {
            Texture2D tex = DownloadHandlerTexture.GetContent(webRequest);
            tex.wrapMode = getTextureIntention.WrapMode;
            tex.filterMode = getTextureIntention.FilterMode;
            return tex;
        }
    }
}
