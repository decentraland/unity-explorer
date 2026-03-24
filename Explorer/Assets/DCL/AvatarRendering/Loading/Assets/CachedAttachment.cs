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
    /// <summary>
    ///     Per-spring-bone data. Chain roots have a non-null <see cref="SkeletonParentName"/> identifying
    ///     the avatar skeleton bone they attach to (e.g. "Neck"). Chain children have null — they follow
    ///     their parent spring bone automatically.
    /// </summary>
    public readonly struct SpringBoneData
    {
        public readonly Transform Transform;
        public readonly string SkeletonParentName;
        public readonly float Stiffness;
        public readonly float Drag;
        public readonly Vector3 GravityDir;
        public readonly float GravityPower;
        public readonly float HitRadius;
        public readonly Quaternion DefaultLocalRotation;
        public readonly Vector3 DefaultLocalPosition;
        public readonly Vector3 DefaultLocalScale;

        public SpringBoneData(Transform transform, string skeletonParentName,
            float stiffness, float drag, Vector3 gravityDir, float gravityPower, float hitRadius,
            Quaternion defaultLocalRotation, Vector3 defaultLocalPosition, Vector3 defaultLocalScale)
        {
            Transform = transform;
            SkeletonParentName = skeletonParentName;
            Stiffness = stiffness;
            Drag = drag;
            GravityDir = gravityDir;
            GravityPower = gravityPower;
            HitRadius = hitRadius;
            DefaultLocalRotation = defaultLocalRotation;
            DefaultLocalPosition = defaultLocalPosition;
            DefaultLocalScale = defaultLocalScale;
        }

        public bool IsChainRoot => SkeletonParentName != null;
    }

    public readonly struct CachedAttachment : IDisposable
    {
        public readonly AttachmentRegularAsset OriginalAsset;
        public readonly GameObject Instance;
        public readonly List<Renderer> Renderers;
        public readonly bool OutlineCompatible;
        public readonly SpringBoneData[] SpringBones;

        public CachedAttachment(AttachmentRegularAsset originalAsset, GameObject instance, bool outlineCompatible,
            SpringBoneData[] springBones = null)
        {
            OriginalAsset = originalAsset;
            Instance = instance;
            Renderers = new List<Renderer>();
            OutlineCompatible = outlineCompatible;
            SpringBones = springBones ?? Array.Empty<SpringBoneData>();

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
