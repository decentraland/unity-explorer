using DCL.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.Loading.Assets
{
    /// <summary>
    ///     We need to store the original asset to be able to release it later
    /// </summary>
    public readonly struct CachedAttachment : IDisposable
    {
        public readonly AttachmentRegularAsset OriginalAsset;
        public readonly GameObject Instance;
        public readonly List<Renderer> Renderers;

        public CachedAttachment(AttachmentRegularAsset originalAsset, GameObject instance)
        {
            OriginalAsset = originalAsset;
            Instance = instance;
            Renderers = new List<Renderer>();

            ProfilingCounters.CachedWearablesAmount.Value++;
        }

        public void Dispose()
        {
            OriginalAsset.Dereference();
            UnityObjectUtils.SafeDestroy(Instance);

            ProfilingCounters.CachedWearablesAmount.Value--;
        }

        public static implicit operator GameObject(CachedAttachment cachedAttachment) =>
            cachedAttachment.Instance;
    }
}
