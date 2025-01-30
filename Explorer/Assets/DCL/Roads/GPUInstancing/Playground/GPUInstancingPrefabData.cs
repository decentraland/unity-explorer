using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancingPrefabData : MonoBehaviour
    {
        public List<GPUInstancingCandidate> candidates;

        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
#if UNITY_EDITOR
            if (transform.position != Vector3.zero)
                transform.position = Vector3.zero;

            if (transform.rotation != Quaternion.identity)
                transform.rotation = Quaternion.identity;

            if (transform.localScale != Vector3.one)
                transform.localScale = Vector3.one;

            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                CollectInstancingCandidates();
#endif
        }

        private void CollectInstancingCandidates()
        {
            foreach (LODGroup lodGroup in gameObject.GetComponentsInChildren<LODGroup>(true))
            {
                UnityEngine.LOD[] lods = lodGroup.GetLODs();

                if (lods.Length == 0 || lods[0].renderers.Length == 0)
                {
                    Debug.LogWarning($"{lodGroup.name} has LODGroup with no lods or no renderers on the first lod level!");
                    continue;
                }

                Matrix4x4 localToRootMatrix = transform.worldToLocalMatrix * lodGroup.transform.localToWorldMatrix; // root * child

                if (!TryAddToCollected(lodGroup, localToRootMatrix))
                    AddNewCandidate(lodGroup, localToRootMatrix);
            }
        }

        private bool TryAddToCollected(LODGroup lodGroup, Matrix4x4 localToRootMatrix)
        {
            foreach (GPUInstancingCandidate existingCandidate in candidates)
            {
                if (IsRenderingDataSame(lodGroup, existingCandidate))
                {
                    Debug.Log($"Same LODGroup: {lodGroup.name} and {existingCandidate.Reference.name}", lodGroup);
                    AddInstance(existingCandidate, localToRootMatrix);
                    return true;
                }
            }

            return false;
        }

        private bool IsRenderingDataSame(LODGroup lodGroup, GPUInstancingCandidate existing)
        {
            // TODO (Vit): validate and log warning for - same names but not same prefab, same prefab but not same Lods, same Lods but MeshRenderer settings are different
            // bool hasSimilarNames = lodGroup.name.Contains(existing.Reference.name) || lodGroup.name.Contains(existing.Reference.name);

            if (AreSamePrefabAsset(lodGroup.gameObject, existing.Reference.gameObject))
                return true;

            UnityEngine.LOD[] lods = lodGroup.GetLODs();

            if (lods.Length != existing.Lods.Count)
            {
                if (HasSimilarName(lodGroup.gameObject, existing.Reference.gameObject))
                    Debug.LogWarning($"{lodGroup.gameObject.name} and {existing.Reference.gameObject.name} has similar name but different lods count", lodGroup.gameObject);

                return false;
            }

            UnityEngine.LOD[] eLods = existing.Reference.GetLODs();

            for (var index = 0; index < lods.Length; index++)
            {
                if (lods[index].renderers.Length != eLods[index].renderers.Length)
                    return false;

                for (var j = 0; j < lods[index].renderers.Length; j++)
                {
                    if (!AreMeshRenderersEquivalent(lods[index].renderers[j] as MeshRenderer, eLods[index].renderers[j] as MeshRenderer))
                        return false;
                }
            }

            return true;
        }

        public static bool AreMeshRenderersEquivalent(MeshRenderer mrA, MeshRenderer mrB)
        {
            if (mrA == null) return false;
            if (mrA.GetComponent<MeshFilter>().sharedMesh != mrB.GetComponent<MeshFilter>().sharedMesh) return false;

            // Compare sharedMaterials.
            Material[] materialsA = mrA.sharedMaterials;
            Material[] materialsB = mrB.sharedMaterials;

            if (materialsA.Length != materialsB.Length)
                return false;

            for (var i = 0; i < materialsA.Length; i++)
                if (materialsA[i] != materialsB[i])
                    return false;

            // Compare relevant MeshRenderer properties
            if (mrA.shadowCastingMode != mrB.shadowCastingMode) return false;
            if (mrA.receiveShadows != mrB.receiveShadows) return false;
            if (mrA.lightProbeUsage != mrB.lightProbeUsage) return false;
            if (mrA.reflectionProbeUsage != mrB.reflectionProbeUsage) return false;
            if (mrA.motionVectorGenerationMode != mrB.motionVectorGenerationMode) return false;

            return true;
        }

        public static bool HasSimilarName(GameObject go1, GameObject go2) =>
            go1.name.Contains(go2.name) || go2.name.Contains(go1.name);

        public static bool AreSamePrefabAsset(GameObject go1, GameObject go2)
        {
            string path1 = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go1);
            string path2 = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go2);

            bool areSame = !string.IsNullOrEmpty(path1) && path1 == path2;

            if (areSame)
                Debug.Log($"Same assets: {go1.name} and {go2.name}", go1);

            return areSame;
        }

        private void AddNewCandidate(LODGroup lodGroup, Matrix4x4 localToRootMatrix)
        {
            if (ValidLODGroup(lodGroup))
            {
                var candidate = new GPUInstancingCandidate(lodGroup, localToRootMatrix);
                candidates.Add(candidate);
            }
        }

        private bool ValidLODGroup(LODGroup lodGroup)
        {
            foreach (UnityEngine.LOD lod in lodGroup.GetLODs())
            {
                foreach (Renderer lodRenderer in lod.renderers)
                {
                    if (lodRenderer == null)
                    {
                        Debug.LogWarning($"{lodGroup.name} has no renderer assigned! Consider removing LODGroup");
                        return false;
                    }

                    if (lodRenderer is not MeshRenderer)
                        return false;
                }
            }

            return true;
        }

        private void AddInstance(GPUInstancingCandidate existingCandidate, Matrix4x4 localToRootMatrix)
        {
            existingCandidate.InstancesBuffer.Add(new PerInstanceBuffer(localToRootMatrix));
        }
    }
}
