using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.Textures
{
    public class TexturesCache : IStreamableCache<Texture2D, GetTextureIntention>
    {
        private readonly Dictionary<GetTextureIntention, Texture2D> cache;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>> OngoingRequests { get; }

        public IDictionary<string, StreamableLoadingResult<Texture2D>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public TexturesCache()
        {
            cache = new Dictionary<GetTextureIntention, Texture2D>(256, this);
            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<Texture2D>>.Get();
            OngoingRequests = DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>>.Get();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            DictionaryPool<string, StreamableLoadingResult<Texture2D>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<Texture2D>>);
            DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>>.Release(OngoingRequests as Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>>);

            disposed = true;
        }

        public bool Equals(GetTextureIntention x, GetTextureIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetTextureIntention obj) =>
            obj.GetHashCode();

        public bool TryGet(in GetTextureIntention key, out Texture2D asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetTextureIntention key, Texture2D asset) =>
            cache.Add(key, asset);

        public void Dereference(in GetTextureIntention key, Texture2D asset) { }
    }
}
