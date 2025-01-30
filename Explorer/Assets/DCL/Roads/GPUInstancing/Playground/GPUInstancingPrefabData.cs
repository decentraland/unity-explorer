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
            foreach (var lodGroup in gameObject.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lodGroup.GetLODs();
                if(lods.Length == 0 || lods[0].renderers.Length == 0) continue;

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
            bool hasSamePrefabReference = false;
            bool hasSameMeshRenderersAssigned = false;

            // TODO (Vit): validate and log warning for - same names but not same prefab, same prefab but not same Lods, same Lods but MeshRenderer settings are different
            // bool hasSimilarNames = lodGroup.name.Contains(existing.Reference.name) || lodGroup.name.Contains(existing.Reference.name);

            if (AreSamePrefabAsset(lodGroup.gameObject, existing.Reference.gameObject))
                return true;

            var lods = lodGroup.GetLODs();
            if (lods.Length != existing.Lods.Count)
            {
                if (HasSimilarName(lodGroup.gameObject, existing.Reference.gameObject))
                    Debug.LogWarning($"{lodGroup.gameObject.name} and {existing.Reference.gameObject.name} has similar name but different lods count", lodGroup.gameObject);

                return false;
            }

            var eLods = existing.Reference.GetLODs();
            for (var index = 0; index < lods.Length; index++)
            {
                if (lods[index].renderers.Length != eLods[index].renderers.Length)
                    return false;

                for (var j = 0; j < lods[index].renderers.Length; j++)
                    if(lods[index].renderers[j] != eLods[index].renderers[j])
                        return false;
            }

            return true;
        }

        public static bool HasSimilarName(GameObject go1, GameObject go2) =>
            go1.name.Contains(go2.name) || go2.name.Contains(go1.name);

        public static bool AreSamePrefabAsset(GameObject go1, GameObject go2)
        {
            string path1 = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go1);
            string path2 = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go2);

            bool areSame = !string.IsNullOrEmpty(path1) && path1 == path2;

            if(areSame)
                Debug.Log($"Same assets: {go1.name} and {go2.name}", go1);

            return areSame;
        }

        private void AddNewCandidate(LODGroup lodGroup, Matrix4x4 localToRootMatrix)
        {
            GPUInstancingCandidate candidate = new GPUInstancingCandidate(lodGroup, localToRootMatrix);
            candidates.Add(candidate);
        }

        private void AddInstance(GPUInstancingCandidate existingCandidate, Matrix4x4 localToRootMatrix)
        {
            existingCandidate.InstancesBuffer.Add(new PerInstanceBuffer(localToRootMatrix));
        }
    }
}
