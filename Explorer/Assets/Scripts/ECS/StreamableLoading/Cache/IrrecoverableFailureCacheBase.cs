using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.Cache
{
    public abstract class IrrecoverableFailureCacheBase<TAsset> : IDisposable
    {
        protected IrrecoverableFailureCacheBase()
        {
            IrrecoverableFailureCacheDictionary = DictionaryPool<string, StreamableLoadingResult<TAsset>>.Get();
        }

        public bool TryGetIrrecoverableFailure(string key, out StreamableLoadingResult<TAsset> irrecoverableFailure) =>
            IrrecoverableFailureCacheDictionary.TryGetValue(key, out irrecoverableFailure);

        public void AddIrrecoverableFailure(string key, StreamableLoadingResult<TAsset> irrecoverableFailure) =>
            IrrecoverableFailureCacheDictionary.Add(key, irrecoverableFailure);

        public void Dispose() =>
            DictionaryPool<string, StreamableLoadingResult<TAsset>>.Release(IrrecoverableFailureCacheDictionary);

        private Dictionary<string, StreamableLoadingResult<TAsset>> IrrecoverableFailureCacheDictionary { get; }
    }
}
