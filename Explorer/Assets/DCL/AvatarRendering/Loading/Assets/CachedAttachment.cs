using DCL.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.Loading.Assets
{
    public readonly struct CachedAttachment : IDisposable
    {
        public readonly AttachmentRegularAsset OriginalAsset;
        public readonly GameObject Instance;
        public readonly List<Renderer> Renderers;
        public readonly bool OutlineCompatible;
        public readonly SpringBoneData[] SpringBones;

        // Matcap preset name for this wearable's metallic materials (from the wearable JSON), resolved
        // to a slice at material setup. Null => default matcap. Optional param keeps existing call sites
        // (tests) unchanged.
        public readonly string? MatcapName;

        public CachedAttachment(AttachmentRegularAsset originalAsset, GameObject instance, bool outlineCompatible, SpringBoneData[] springBones, string? matcapName = null)
        {
            OriginalAsset = originalAsset;
            Instance = instance;
            Renderers = new List<Renderer>();
            OutlineCompatible = outlineCompatible;
            SpringBones = springBones;
            MatcapName = matcapName;

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
