using UnityEngine;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Ocean
{
    /// <summary>
    ///     Per-tile ocean component on every WaterTile child of Ocean.prefab. Holds
    ///     the tile's authored ocean material plus its MeshFilter/MeshRenderer, so
    ///     the sibling <see cref="OceanProceduralMesh"/> on the parent can hand every
    ///     tile the shared tessellated grid mesh through <see cref="MeshFilterRef"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class OceanMeshController : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;

        /// <summary>
        ///     The tile's MeshFilter. Resolved on first access so the parent grid
        ///     builder does not depend on this component's Awake having run.
        /// </summary>
        public MeshFilter MeshFilterRef => meshFilter != null ? meshFilter : meshFilter = GetComponent<MeshFilter>();

        private void Awake()
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            if (material != null && meshRenderer != null)
                meshRenderer.sharedMaterial = material;
        }
    }
}
