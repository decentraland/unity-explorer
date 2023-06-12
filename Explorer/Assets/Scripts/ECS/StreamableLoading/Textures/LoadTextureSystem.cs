using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class LoadTextureSystem : LoadSystemBase<Texture2D, GetTextureIntention>
    {
        internal LoadTextureSystem(World world, IStreamableCache<Texture2D, GetTextureIntention> cache) : base(world, cache) { }

        protected override async UniTask<StreamableLoadingResult<Texture2D>> FlowInternal(GetTextureIntention intention, CancellationToken ct)
        {
            UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(intention.CommonArguments.URL, !intention.IsReadable);
            await webRequest.SendWebRequest().WithCancellation(ct);
            Texture2D tex = DownloadHandlerTexture.GetContent(webRequest);
            tex.wrapMode = intention.WrapMode;
            tex.filterMode = intention.FilterMode;
            return new StreamableLoadingResult<Texture2D>(tex);
        }
    }
}
