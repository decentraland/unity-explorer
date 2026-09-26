using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace SpringBones
{
    public class SpringBonesDriver : MonoBehaviour
    {
        SpringBoneService service;
        readonly List<int> slotIndices = new();
        readonly List<Transform> chainWearableParents = new();
        readonly List<Transform> chainAvatarParents = new();
        readonly List<GameObject> chainOwners = new();
        readonly List<string> chainRootBoneNames = new();
        readonly List<Transform[]> chainBones = new();
        readonly List<Quaternion[]> chainInitialRotations = new();

        readonly List<Transform> chainJoints = new();
        readonly List<SpringBoneJointConfig> chainConfigs = new();

        // Live avatar skeleton bones keyed by name. Used to find the avatar bone that the
        // spring chain's wearable parent should follow each frame.
        public IReadOnlyDictionary<string, Transform> AvatarBoneMap { get; set; }

        void Awake() => service = new SpringBoneService();

        void OnDestroy()
        {
            service?.Dispose();
            service = null;
        }

        public void UnregisterAll()
        {
            if (service == null) return;
            // Restore each chain's bones to their captured rest pose before clearing,
            // otherwise re-registration would capture stale sim rotations as the new rest.
            for (int i = 0; i < slotIndices.Count; i++)
            {
                var bones = chainBones[i];
                var rots = chainInitialRotations[i];
                for (int j = 0; j < bones.Length; j++)
                    if (bones[j] != null) bones[j].localRotation = rots[j];
                service.UnregisterSpring(slotIndices[i]);
            }
            slotIndices.Clear();
            chainWearableParents.Clear();
            chainAvatarParents.Clear();
            chainOwners.Clear();
            chainRootBoneNames.Clear();
            chainBones.Clear();
            chainInitialRotations.Clear();
        }

        /// <summary>
        /// Replace the full set of spring chains for a wearable with the ones declared in
        /// <paramref name="paramsByBone"/>. Empty / null map clears all chains owned by
        /// <paramref name="owner"/>, restoring the bones to their captured rest pose (normal bones).
        /// Every entry in <paramref name="paramsByBone"/> becomes its own chain root; descendants
        /// are collected by walking first-child until no children remain or the joint cap is hit.
        /// Untagged bones stay rigid (driven by their armature parent via normal skinning).
        /// </summary>
        public void SetSpringChainsForWearable(GameObject owner, Dictionary<string, SpringBoneParamsDTO> paramsByBone)
        {
            if (service == null || owner == null) return;

            RemoveChainsForOwner(owner);

            if (paramsByBone == null || paramsByBone.Count == 0) return;

            var skinned = owner.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinned == null)
            {
                Debug.LogError($"[SpringBones] '{owner.name}' has no SkinnedMeshRenderer; cannot build chains");
                return;
            }

            var bonesByName = new Dictionary<string, Transform>(skinned.bones.Length);
            foreach (var b in skinned.bones)
                if (b != null) bonesByName[b.name] = b;

            foreach (var (boneName, paramsDto) in paramsByBone)
            {
                if (!bonesByName.TryGetValue(boneName, out var rootBone)) continue;

                chainJoints.Clear();
                chainConfigs.Clear();
                var rootConfig = BuildConfigFromDTO(paramsDto, rootBone.localRotation);
                chainJoints.Add(rootBone);
                chainConfigs.Add(rootConfig);

                // Linear first-child walk. Tagged bone is the chain anchor; armature sits above it
                // and is never visited because traversal only goes down. Untagged descendants are
                // pulled in as joints sharing the root's params.
                var current = rootBone;
                while (chainJoints.Count < SpringBoneService.MAX_JOINTS_PER_SPRING && current.childCount > 0)
                {
                    var next = current.GetChild(0);
                    var c = rootConfig;
                    c.LocalRotation = next.localRotation;
                    chainJoints.Add(next);
                    chainConfigs.Add(c);
                    current = next;
                }

                Transform avatarParent = null;
                if (rootBone.parent != null && AvatarBoneMap != null)
                    AvatarBoneMap.TryGetValue(rootBone.parent.name, out avatarParent);

                // Snap wearable parent to live avatar bone NOW so chain tails get initialized at
                // the correct world positions (otherwise first sim frame integrates against stale
                // wearable-hierarchy positions). Also align scale so authored local positions
                // translate to the expected world distances.
                if (avatarParent != null && rootBone.parent != null && rootBone.parent != avatarParent)
                {
                    var grandparent = rootBone.parent.parent;
                    var grandparentLossy = grandparent != null ? grandparent.lossyScale : Vector3.one;
                    var avatarLossy = avatarParent.lossyScale;
                    rootBone.parent.localScale = new Vector3(
                        SafeDiv(avatarLossy.x, grandparentLossy.x),
                        SafeDiv(avatarLossy.y, grandparentLossy.y),
                        SafeDiv(avatarLossy.z, grandparentLossy.z));
                    rootBone.parent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);
                }

                FlushChain(owner, rootBone.parent, avatarParent, rootBone.name);
            }
        }

        static float SafeDiv(float a, float b) => Mathf.Abs(b) > 1e-6f ? a / b : 1f;

        int RemoveChainsForOwner(GameObject owner)
        {
            int removed = 0;
            for (int i = slotIndices.Count - 1; i >= 0; i--)
            {
                if (chainOwners[i] != owner) continue;

                var bones = chainBones[i];
                var rots = chainInitialRotations[i];
                for (int j = 0; j < bones.Length; j++)
                    if (bones[j] != null) bones[j].localRotation = rots[j];

                service.UnregisterSpring(slotIndices[i]);
                slotIndices.RemoveAt(i);
                chainWearableParents.RemoveAt(i);
                chainAvatarParents.RemoveAt(i);
                chainOwners.RemoveAt(i);
                chainRootBoneNames.RemoveAt(i);
                chainBones.RemoveAt(i);
                chainInitialRotations.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        void FlushChain(GameObject owner, Transform wearableParent, Transform avatarParent, string rootBoneName)
        {
            for (int j = 0; j < chainJoints.Count; j++)
            {
                var c = chainConfigs[j];
                Transform tail = j + 1 < chainJoints.Count ? chainJoints[j + 1] : null;

                if (tail != null)
                {
                    float3 localPos = (float3)tail.localPosition;
                    float3 scale = (float3)tail.lossyScale;
                    float3 scaledPos = localPos * scale;
                    float len = math.length(scaledPos);
                    c.BoneAxis = len > 0.0001f ? scaledPos / len : new float3(0, 1, 0);
                    c.Length = len;
                }
                else
                {
                    c.BoneAxis = new float3(0, 1, 0);
                    c.Length = 0.1f;
                }

                chainConfigs[j] = c;
            }

            var tails = new float3[chainJoints.Count];
            for (int j = 0; j < chainJoints.Count; j++)
                tails[j] = j + 1 < chainJoints.Count ? (float3)chainJoints[j + 1].position : (float3)chainJoints[j].position;

            var jointsCopy = chainJoints.ToArray();
            var initRots = new Quaternion[jointsCopy.Length];
            for (int j = 0; j < jointsCopy.Length; j++) initRots[j] = jointsCopy[j].localRotation;

            int slot = service.RegisterSpring(jointsCopy, chainConfigs.ToArray(), tails);
            slotIndices.Add(slot);
            chainWearableParents.Add(wearableParent);
            chainAvatarParents.Add(avatarParent);
            chainOwners.Add(owner);
            chainRootBoneNames.Add(rootBoneName);
            chainBones.Add(jointsCopy);
            chainInitialRotations.Add(initRots);
        }

        static SpringBoneJointConfig BuildConfigFromDTO(SpringBoneParamsDTO p, Quaternion initialRotation)
        {
            float3 gravityDir = p.gravityDir != null && p.gravityDir.Length == 3
                ? new float3(p.gravityDir[0], p.gravityDir[1], p.gravityDir[2])
                : new float3(0, -1, 0);
            return new SpringBoneJointConfig
            {
                Stiffness = p.stiffness,
                Drag = p.drag,
                GravityDir = gravityDir,
                GravityPower = p.gravityPower,
                LocalRotation = initialRotation,
            };
        }

        void LateUpdate()
        {
            if (service == null || slotIndices.Count == 0) return;

            for (int i = 0; i < slotIndices.Count; i++)
            {
                var wearableParent = chainWearableParents[i];
                var avatarParent = chainAvatarParents[i];
                if (avatarParent == null)
                {
                    // No live avatar bone match — fall back to whatever the chain root's parent is.
                    if (wearableParent == null) continue;
                    service.SetParentData(slotIndices[i], wearableParent.rotation, wearableParent.localToWorldMatrix);
                    continue;
                }

                // Snap the wearable's intermediate parent to follow the live avatar bone every frame.
                // Spring bone is a child of wearableParent, so its world transform follows the avatar
                // while preserving the authored local pose under the wearable hierarchy.
                if (wearableParent != null && wearableParent != avatarParent)
                {
                    var grandparent = wearableParent.parent;
                    var grandparentLossy = grandparent != null ? grandparent.lossyScale : Vector3.one;
                    var avatarLossy = avatarParent.lossyScale;
                    wearableParent.localScale = new Vector3(
                        SafeDiv(avatarLossy.x, grandparentLossy.x),
                        SafeDiv(avatarLossy.y, grandparentLossy.y),
                        SafeDiv(avatarLossy.z, grandparentLossy.z));
                    wearableParent.SetPositionAndRotation(avatarParent.position, avatarParent.rotation);
                }

                service.SetParentData(slotIndices[i], avatarParent.rotation, avatarParent.localToWorldMatrix);
            }

            service.Simulate(Time.deltaTime);
        }
    }
}
