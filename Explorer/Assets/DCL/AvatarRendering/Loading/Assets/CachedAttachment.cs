using DCL.Profiling;
using System;
using System.Collections.Generic;
using UniGLTF.SpringBoneJobs.InputPorts;
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

        public CachedAttachment(AttachmentRegularAsset originalAsset, GameObject instance, bool outlineCompatible, SpringBoneData[] springBones)
        {
            OriginalAsset = originalAsset;
            Instance = instance;
            Renderers = new List<Renderer>();
            OutlineCompatible = outlineCompatible;
            SpringBones = springBones;

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
