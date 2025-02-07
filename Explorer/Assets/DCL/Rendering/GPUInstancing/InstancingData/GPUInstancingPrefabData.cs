using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancingPrefabData : MonoBehaviour
    {
        public List<GPUInstancingCandidate> indirectCandidates;
        public List<GPUInstancingCandidate> directCandidates;

        public List<Renderer> InstancedRenderers;
        public List<LODGroup> InstancedLODGroups;

        public Shader[] whitelistShaders;

        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
#if UNITY_EDITOR
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);

            if (string.IsNullOrEmpty(assetPath))
                return;

            // AssetDatabase.Refresh();

            // FIX (VIt): we need to open prefab in stage mode, so Unity correctly recognize shaders (otherwise sometimes it will use just default Lit shader and ruin our ValidShader check)
            PrefabStage prefabStage = PrefabStageUtility.OpenPrefab(assetPath);

            foreach (Shader whitelistedShader in whitelistShaders)
            {
                string shaderPath = AssetDatabase.GetAssetPath(whitelistedShader);
                Shader loadedShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            }
            GameObject prefabRoot = prefabStage.prefabContentsRoot;

            // Force shader reload on all materials
            foreach (MeshRenderer renderer in prefabRoot.GetComponentsInChildren<MeshRenderer>())
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null)
                {
                    string shaderName = mat.shader.name;
                    var m = new Material(Shader.Find(shaderName));
                }
            }

            AssetDatabase.Refresh();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    GPUInstancingPrefabData instanceComponent = prefabRoot.GetComponent<GPUInstancingPrefabData>();

                    instanceComponent.SetPrefabRootTransformToZero();
                    instanceComponent.indirectCandidates = new List<GPUInstancingCandidate>();
                    instanceComponent.directCandidates = new List<GPUInstancingCandidate>();
                    instanceComponent.InstancedRenderers = new List<Renderer>();
                    instanceComponent.InstancedLODGroups = new List<LODGroup>();
                    instanceComponent.CollectInstancingCandidatesFromStandaloneMeshes(instanceComponent.GetComponentsInChildren<MeshRenderer>(false));
                    instanceComponent.CollectInstancingCandidatesFromLODGroups(instanceComponent.GetComponentsInChildren<LODGroup>(false));

                    EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                    StageUtility.GoToMainStage();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error during collection: {e}");
                    StageUtility.GoToMainStage();
                }
            };
#endif
        }

        private void SetPrefabRootTransformToZero()
        {
            if (transform.position != Vector3.zero) transform.position = Vector3.zero;
            if (transform.rotation != Quaternion.identity) transform.rotation = Quaternion.identity;
            if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
        }

        private void SavePrefabChanges()
        {
#if UNITY_EDITOR
            if (!PrefabUtility.IsPartOfPrefabAsset(gameObject))
                return;

            EditorUtility.SetDirty(this);

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);

            if (!string.IsNullOrEmpty(assetPath))
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
#endif
        }

        [ContextMenu(nameof(HideVisuals))]
        public void HideVisuals()
        {
            foreach (Renderer instancedRenderer in InstancedRenderers) { instancedRenderer.enabled = false; }
        }

        public void ShowVisuals()
        {
            foreach (Renderer instancedRenderer in InstancedRenderers) instancedRenderer.enabled = true;
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
                    || !GPUInstancingCandidate.IsValidShader(mr.sharedMaterials, whitelistShaders))
                    continue;

                if (!AssignedToLODGroupInPrefabHierarchy(mr))
                {
                    Debug.Log($"0 mesh {mr.transform.name} is without LOD", mr.gameObject);

                    if (!GPUInstancingCandidate.IsValidShader(mr.sharedMaterials, whitelistShaders))
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

        private bool TryAddSingleMeshToCollected(MeshRenderer meshRenderer, Matrix4x4 localToRootMatrix, List<GPUInstancingCandidate> collectedCandidates)
        {
            foreach (GPUInstancingCandidate existingCandidate in collectedCandidates)
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

        private bool TryAddToCollected(LODGroup lodGroup, Matrix4x4 localToRootMatrix, List<GPUInstancingCandidate> collectedCandidates)
        {
            foreach (GPUInstancingCandidate existingCandidate in collectedCandidates)
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

            if (existing.Reference == null) return false;

            // if (AreSamePrefabAsset(lodGroup.gameObject, existing.Reference.gameObject)) return true;

            LOD[] lods = lodGroup.GetLODs();

            if (lods.Length != existing.Lods.Count)
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

        private bool IsSingleRenderingDataSame(MeshRenderer meshRenderer, GPUInstancingCandidate existing) =>
            AreMeshRenderersEquivalent(meshRenderer, existing.Lods[0].MeshRenderingDatas[0].Renderer);

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

        private void AddNewCandidate(LODGroup lodGroup, Matrix4x4 localToRootMatrix, List<GPUInstancingCandidate> collectedCandidates)
        {
            if (ValidLODGroup(lodGroup))
                collectedCandidates.Add(new GPUInstancingCandidate(lodGroup, localToRootMatrix, whitelistShaders));
        }

        private void AddNewStandaloneMeshCandidate(MeshRenderer meshRenderer, Matrix4x4 localToRootMatrix, List<GPUInstancingCandidate> collectedCandidates) =>
            collectedCandidates.Add(new GPUInstancingCandidate(meshRenderer, localToRootMatrix, whitelistShaders));

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

        private void AddInstance(GPUInstancingCandidate existingCandidate, Matrix4x4 localToRootMatrix)
        {
            existingCandidate.InstancesBuffer.Add(new PerInstanceBuffer(localToRootMatrix));
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
    }
}
