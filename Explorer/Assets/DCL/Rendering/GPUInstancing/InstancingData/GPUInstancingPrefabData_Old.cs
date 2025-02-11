using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancingPrefabData_Old : MonoBehaviour
    {
        public List<GPUInstancingCandidate_Old> indirectCandidates;
        public List<GPUInstancingCandidate_Old> directCandidates;

        public List<Renderer> InstancedRenderers;
        public List<LODGroup> InstancedLODGroups;

        public Shader[] whitelistShaders;

        private void SetPrefabRootTransformToZero()
        {
            if (transform.position != Vector3.zero) transform.position = Vector3.zero;
            if (transform.rotation != Quaternion.identity) transform.rotation = Quaternion.identity;
            if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
        }

        [ContextMenu(nameof(HideVisuals))]
        public void HideVisuals()
        {
            foreach (Renderer instancedRenderer in InstancedRenderers) { instancedRenderer.enabled = false; }
        }

        public void ShowVisuals()
        {
            return;

            if (InstancedRenderers != null)
                foreach (Renderer instancedRenderer in InstancedRenderers)
                    instancedRenderer.enabled = true;

            foreach (LODGroup lodGroup in InstancedLODGroups) lodGroup.enabled = true;
        }

        private void CollectInstancingCandidatesFromLODGroups(LODGroup[] lodGroups)
        {
            foreach (LODGroup lodGroup in lodGroups)
            {
                LOD[] lods = lodGroup.GetLODs();

                if (lods.Length == 0 || lods[0].renderers.Length == 0 || lods[0].renderers[0] == null || lods[0].renderers[0].sharedMaterials.Length == 0)
                {
                    Debug.LogWarning($"{lodGroup.name} has LODGroup with no lods/renderers/materials on the first lod level! Please revise the prefab.");
                    continue;
                }

                var validLodLevels = 0;

                foreach (LOD lod in lods)
                {
                    var addedRenderers = 0;

                    foreach (Renderer lodRenderer in lod.renderers)
                    {
                        if (lodRenderer is MeshRenderer) // && IsValidShader(lodRenderer.sharedMaterials))
                        {
                            InstancedRenderers.Add(lodRenderer);
                            addedRenderers++;
                        }
                    }

                    if (addedRenderers == lod.renderers.Length)
                        validLodLevels++;
                }

                if (validLodLevels == lods.Length)
                    InstancedLODGroups.Add(lodGroup);

                Matrix4x4 localToRootMatrix = transform.worldToLocalMatrix * lodGroup.transform.localToWorldMatrix; // root * child

                // List<GPUInstancingCandidate> collectedCandidates = IsMyShader(lods[0].renderers[0].sharedMaterials) ? indirectCandidates : directCandidates;
                if (!TryAddToCollected(lodGroup, localToRootMatrix, indirectCandidates))
                    AddNewCandidate(lodGroup, localToRootMatrix, indirectCandidates);
            }
        }

        private void CollectInstancingCandidatesFromStandaloneMeshes(MeshRenderer[] meshRenderers)
        {
            foreach (MeshRenderer mr in meshRenderers)
            {
                if (mr == null || mr.sharedMaterial == null || mr.GetComponent<MeshFilter>().sharedMesh == null
                    || !mr.gameObject.activeSelf || mr.gameObject.name.EndsWith("_collider")
                    || !GPUInstancingCandidate_Old.IsValidShader(mr.sharedMaterials, whitelistShaders))
                    continue;

                if (!AssignedToLODGroupInPrefabHierarchy(mr))
                {
                    Debug.Log($"0 mesh {mr.transform.name} is without LOD", mr.gameObject);

                    if (!GPUInstancingCandidate_Old.IsValidShader(mr.sharedMaterials, whitelistShaders))
                    {
                        Debug.Log($"1.0 mesh {mr.transform.name} has no valid shader {mr.sharedMaterial.shader.name}", mr.gameObject);
                        continue;
                    }

                    Debug.Log($"1.1 mesh {mr.transform.name} is valid, adding it", mr.gameObject);

                    InstancedRenderers.Add(mr);

                    Matrix4x4 localToRootMatrix = transform.worldToLocalMatrix * mr.transform.localToWorldMatrix; // root * child

                    // List<GPUInstancingCandidate> collectedCandidates = IsMyShader(mr.sharedMaterials) ? indirectCandidates : directCandidates;
                    if (!TryAddSingleMeshToCollected(mr, localToRootMatrix, indirectCandidates))
                        AddNewStandaloneMeshCandidate(mr, localToRootMatrix, indirectCandidates);
                }
            }
        }

        private bool TryAddSingleMeshToCollected(MeshRenderer meshRenderer, Matrix4x4 localToRootMatrix, List<GPUInstancingCandidate_Old> collectedCandidates)
        {
            foreach (GPUInstancingCandidate_Old existingCandidate in collectedCandidates)
            {
                if (IsSingleRenderingDataSame(meshRenderer, existingCandidate))
                {
                    Debug.Log($"Same single mesh: {meshRenderer.name} and {existingCandidate.Transform.name}", meshRenderer);
                    AddInstance(existingCandidate, localToRootMatrix);
                    return true;
                }
            }

            return false;
        }

        private bool TryAddToCollected(LODGroup lodGroup, Matrix4x4 localToRootMatrix, List<GPUInstancingCandidate_Old> collectedCandidates)
        {
            foreach (GPUInstancingCandidate_Old existingCandidate in collectedCandidates)
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

        private bool IsRenderingDataSame(LODGroup lodGroup, GPUInstancingCandidate_Old existing)
        {
            // TODO (Vit): validate and log warning for - same names but not same prefab, same prefab but not same Lods, same Lods but MeshRenderer settings are different
            // bool hasSimilarNames = lodGroup.name.Contains(existing.Reference.name) || lodGroup.name.Contains(existing.Reference.name);

            if (existing.Reference == null) return false;

            // if (AreSamePrefabAsset(lodGroup.gameObject, existing.Reference.gameObject)) return true;

            LOD[] lods = lodGroup.GetLODs();

            if (lods.Length != existing.Lods_Old.Count)
            {
                if (HasSimilarName(lodGroup.gameObject, existing.Reference.gameObject))
                    Debug.LogWarning($"{lodGroup.gameObject.name} and {existing.Reference.gameObject.name} has similar name but different lods count", lodGroup.gameObject);

                return false;
            }

            LOD[] eLods = existing.Reference.GetLODs();

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

        private bool IsSingleRenderingDataSame(MeshRenderer meshRenderer, GPUInstancingCandidate_Old existing) =>
            AreMeshRenderersEquivalent(meshRenderer, existing.Lods_Old[0].MeshRenderingDatas[0].Renderer);

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
            if (go1 == null || go2 == null) return false;

            string path1 = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go1);
            string path2 = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go2);

            bool areSame = !string.IsNullOrEmpty(path1) && path1 == path2;

            if (areSame)
                Debug.Log($"Same assets: {go1.name} and {go2.name}", go1);

            return areSame;
        }

        private void AddNewCandidate(LODGroup lodGroup, Matrix4x4 localToRootMatrix, List<GPUInstancingCandidate_Old> collectedCandidates)
        {
            if (ValidLODGroup(lodGroup))
                collectedCandidates.Add(new GPUInstancingCandidate_Old(lodGroup, localToRootMatrix, whitelistShaders));
        }

        private void AddNewStandaloneMeshCandidate(MeshRenderer meshRenderer, Matrix4x4 localToRootMatrix, List<GPUInstancingCandidate_Old> collectedCandidates) =>
            collectedCandidates.Add(new GPUInstancingCandidate_Old(meshRenderer, localToRootMatrix, whitelistShaders));

        private bool ValidLODGroup(LODGroup lodGroup)
        {
            foreach (LOD lod in lodGroup.GetLODs())
            foreach (Renderer lodRenderer in lod.renderers)
            {
                if (lodRenderer == null)
                {
                    Debug.LogWarning($"{lodGroup.name} has no renderer assigned! Consider removing LODGroup");
                    return false;
                }
            }

            return true;
        }

        private void AddInstance(GPUInstancingCandidate_Old existingLODGroup, Matrix4x4 localToRootMatrix)
        {
            existingLODGroup.InstancesBuffer.Add(new PerInstanceBuffer(localToRootMatrix));
        }

        private bool AssignedToLODGroupInPrefabHierarchy(MeshRenderer renderer)
        {
            Transform current = renderer.transform;

            while (current != transform && current != null)
            {
                if (IsAssignedToTheGroup(current.GetComponent<LODGroup>(), renderer))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private bool IsAssignedToTheGroup(LODGroup getComponent, MeshRenderer meshRenderer)
        {
            return getComponent != null && getComponent.GetLODs().Any(lod => lod.renderers.Any(lodRenderer => meshRenderer == lodRenderer));
        }

#if UNITY_EDITOR
        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);

            if (string.IsNullOrEmpty(assetPath))
                return;

            // Start the collection coroutine
            EditorCoroutineUtility.StartCoroutine(CollectDataWithPrefabInstance(assetPath), this);
        }

        private IEnumerator CollectDataWithPrefabInstance(string assetPath)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var tempInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);

            try
            {
                // Position at origin
                tempInstance.transform.position = Vector3.zero;
                tempInstance.transform.rotation = Quaternion.identity;
                tempInstance.transform.localScale = Vector3.one;

                // Wait 2 frames to ensure proper initialization and camera has rendered
                for (var i = 0; i < 2; i++) yield return new WaitForEndOfFrame();

                GPUInstancingPrefabData_Old instanceComponent = tempInstance.GetComponent<GPUInstancingPrefabData_Old>();

                // Clear and collect new data
                instanceComponent.indirectCandidates = new List<GPUInstancingCandidate_Old>();
                instanceComponent.directCandidates = new List<GPUInstancingCandidate_Old>();
                instanceComponent.InstancedRenderers = new List<Renderer>();
                instanceComponent.InstancedLODGroups = new List<LODGroup>();

                // Collect the data
                instanceComponent.CollectInstancingCandidatesFromStandaloneMeshes(instanceComponent.GetComponentsInChildren<MeshRenderer>(false));
                instanceComponent.CollectInstancingCandidatesFromLODGroups(instanceComponent.GetComponentsInChildren<LODGroup>(false));

                // Remap all references from instance to prefab
                RemapReferencesToPrefab(instanceComponent, tempInstance, prefabAsset);

                // Copy the collected data back to the original prefab
                GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                GPUInstancingPrefabData_Old originalComponent = originalPrefab.GetComponent<GPUInstancingPrefabData_Old>();
                EditorUtility.CopySerialized(instanceComponent, originalComponent);

                // Save the changes
                EditorUtility.SetDirty(originalComponent);
                AssetDatabase.SaveAssets();

                yield return new WaitForEndOfFrame();
            }
            finally { DestroyImmediate(tempInstance); }
        }

        private void RemapReferencesToPrefab(GPUInstancingPrefabData_Old instanceComponent, GameObject instance, GameObject prefab)
        {
            // Remap InstancedRenderers
            instanceComponent.InstancedRenderers = RemapComponentReferences(
                instanceComponent.InstancedRenderers, instance, prefab);

            // Remap InstancedLODGroups
            instanceComponent.InstancedLODGroups = RemapComponentReferences(
                instanceComponent.InstancedLODGroups, instance, prefab);

            // Remap Candidates
            foreach (GPUInstancingCandidate_Old candidate in instanceComponent.indirectCandidates) { RemapCandidate(candidate, instance, prefab); }

            foreach (GPUInstancingCandidate_Old candidate in instanceComponent.directCandidates) { RemapCandidate(candidate, instance, prefab); }
        }

        private List<T> RemapComponentReferences<T>(List<T> instanceComponents, GameObject instance, GameObject prefab) where T: Component
        {
            var remappedComponents = new List<T>();

            foreach (T component in instanceComponents)
            {
                string relativePath = GetRelativePath(instance.transform, component.transform);
                Transform prefabTransform = prefab.transform.Find(relativePath);

                if (prefabTransform != null)
                {
                    T prefabComponent = prefabTransform.GetComponent<T>();

                    if (prefabComponent != null) { remappedComponents.Add(prefabComponent); }
                }
            }

            return remappedComponents;
        }

        private void RemapCandidate(GPUInstancingCandidate_Old lodGroup, GameObject instance, GameObject prefab)
        {
            // Remap Transform
            if (lodGroup.Transform != null)
            {
                string transformPath = GetRelativePath(instance.transform, lodGroup.Transform);
                lodGroup.Transform = prefab.transform.Find(transformPath);
            }

            // Remap LODGroup reference
            if (lodGroup.Reference != null)
            {
                string referencePath = GetRelativePath(instance.transform, lodGroup.Reference.transform);
                Transform prefabTransform = prefab.transform.Find(referencePath);
                lodGroup.Reference = prefabTransform?.GetComponent<LODGroup>();
            }

            // Remap MeshRenderingData references in each LOD level
            if (lodGroup.Lods_Old != null)
            {
                foreach (GPUInstancingLodLevel_Old lod in lodGroup.Lods_Old)
                {
                    if (lod.MeshRenderingDatas != null)
                    {
                        foreach (MeshRenderingData_Old renderData in lod.MeshRenderingDatas)
                        {
                            if (renderData.Renderer != null)
                            {
                                string rendererPath = GetRelativePath(instance.transform, renderData.Renderer.transform);
                                Transform prefabTransform = prefab.transform.Find(rendererPath);
                                renderData.Renderer = prefabTransform?.GetComponent<MeshRenderer>();
                            }
                        }
                    }
                }
            }
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            var path = new StringBuilder();
            Transform current = target;

            while (current != null && current != root)
            {
                if (path.Length > 0) { path.Insert(0, "/"); }

                path.Insert(0, current.name);
                current = current.parent;
            }

            return path.ToString();
        }
#endif
    }
}
