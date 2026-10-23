using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Loading.Assets;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.BevyMigration.Bridge
{
    /// <summary>
    ///     Registers avatars for the bridge (BevyMigration/docs/README.md § 7, step 4): one skeleton per avatar (the
    ///     62 base bones, then every wearable's spring bones) and one skinned renderer per wearable mesh, with the
    ///     wearable's original material. Bevy skins in the vertex shader from the bone matrices in the snapshot.
    /// </summary>
    public class AvatarContentScanner
    {
        private const int BASE_BONE_COUNT = 62;

        private readonly RendererRegistry registry;
        private readonly ResourceUploader uploader;
        private readonly List<Transform> joints = new (128);

        public AvatarContentScanner(RendererRegistry registry, ResourceUploader uploader)
        {
            this.registry = registry;
            this.uploader = uploader;
        }

        public int Avatars { get; private set; }

        /// <summary>Call once per scan before <see cref="Register" /> runs over every avatar.</summary>
        public void Begin()
        {
            registry.ClearSkeletons();
            Avatars = 0;
        }

        public void Register(in AvatarShapeComponent shape, AvatarBase avatarBase)
        {
            Transform[] baseBones = avatarBase.AvatarSkinnedMeshRenderer.bones;
            if (baseBones.Length < BASE_BONE_COUNT || shape.InstantiatedWearables.Count == 0) return;

            joints.Clear();

            for (var i = 0; i < BASE_BONE_COUNT; i++)
                joints.Add(baseBones[i]);

            foreach (CachedAttachment wearable in shape.InstantiatedWearables)
                foreach (SpringBoneData spring in wearable.SpringBones)
                    joints.Add(spring.ManagedTransform);

            int skeleton = registry.RegisterSkeleton(joints);
            var springOffset = 0;
            Avatars++;

            foreach (CachedAttachment wearable in shape.InstantiatedWearables)
            {
                IReadOnlyList<AttachmentRegularAsset.RendererInfo> infos = wearable.OriginalAsset.RendererInfos;

                for (var i = 0; i < wearable.Renderers.Count; i++)
                {
                    Renderer renderer = wearable.Renderers[i];
                    if (renderer == null) continue;

                    if (registry.Contains(renderer))
                    {
                        registry.SetSkeleton(renderer, skeleton);
                        continue;
                    }

                    MeshFilter? filter = renderer.GetComponent<MeshFilter>();
                    Mesh? mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null) continue;

                    uint meshHandle = uploader.SkinnedMesh(mesh, springOffset, joints.Count);
                    Material? original = i < infos.Count ? infos[i].Material : null;
                    uint[] materials = { original != null ? uploader.Material(original) : BridgeMaterials.PLACEHOLDER };
                    registry.Register(renderer, meshHandle, materials, skeleton);
                }

                springOffset += wearable.SpringBones.Length;
            }
        }
    }
}
