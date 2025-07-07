using DCL.Utilities.Extensions;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Frames
{
    public class MeshFrame : AbstractFrame
    {
        [SerializeField] private UnityEngine.Renderer renderer = null!;
        [SerializeField] private Material viewNftShapeMaterial = null!;
        [SerializeField] private Texture2D emptyTexture = null!;
        [SerializeField] private string albedoColorPropertyName = "_BaseMap";
        [SerializeField] private string texScalePropertyName = "_BaseMap_ST";

        [SerializeField] private int placeIndex;

        [Header("Status")]
        [SerializeField] private GameObject loadingStatus = null!;
        [SerializeField] private GameObject failedStatus = null!;

        [Header("Backplate")]
        [SerializeField] private int[] backplateIndexes = Array.Empty<int>();
        [SerializeField] private string backplateColorPropertyName = "_BaseColor";

        private int albedoColorPropertyId;
        private int backplateColorPropertyId;
        private int texScalePropertyId;
        private MaterialPropertyBlock backplateMaterialPropertyBlock = null!;
        private MaterialPropertyBlock viewNftMaterialPropertyBlock = null!;

        public void Awake()
        {
            renderer.EnsureNotNull();

            // Enforce renderer scale for backward compatibility (hardcoded in unity-renderer)
            renderer.transform.localScale = new Vector3(0.5f, 0.5f, 1);

            emptyTexture.EnsureNotNull();
            viewNftShapeMaterial.EnsureNotNull();
            loadingStatus.EnsureNotNull();
            failedStatus.EnsureNotNull();
            backplateMaterialPropertyBlock = new MaterialPropertyBlock();
            viewNftMaterialPropertyBlock = new MaterialPropertyBlock();
            backplateColorPropertyId = Shader.PropertyToID(backplateColorPropertyName);
            albedoColorPropertyId = Shader.PropertyToID(albedoColorPropertyName);
            texScalePropertyId = Shader.PropertyToID(texScalePropertyName);

            ApplyCanvasMaterial(viewNftShapeMaterial);
            HideStatuses();
        }

        public override void Paint(Color color)
        {
            backplateMaterialPropertyBlock.SetColor(backplateColorPropertyId, color);

            foreach (int backplateIndex in backplateIndexes)
                renderer.SetPropertyBlock(backplateMaterialPropertyBlock, backplateIndex);
        }

        public override void Place(Texture2D picture)
        {
            viewNftMaterialPropertyBlock.SetTexture(albedoColorPropertyId, picture);
            renderer.SetPropertyBlock(viewNftMaterialPropertyBlock, placeIndex);
            HideStatuses();
        }

        public override void UpdateStatus(Status status)
        {
            Place(emptyTexture);
            HideStatuses();

            var current = status switch
                          {
                              Status.Loading => loadingStatus,
                              Status.Failed => failedStatus,
                              _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
                          };

            current.SetActive(true);
        }

        public override void SetTextureScale(Vector2 texScale)
        {
            viewNftMaterialPropertyBlock.SetVector(texScalePropertyId, new Vector4(texScale.x, texScale.y, 0, 0));
            renderer.SetPropertyBlock(viewNftMaterialPropertyBlock, placeIndex);
        }

        private void ApplyCanvasMaterial(Material material)
        {
            var materials = renderer.materials!;
            materials[placeIndex] = material;
            renderer.materials = materials;
        }

        private void HideStatuses()
        {
            loadingStatus.SetActive(false);
            failedStatus.SetActive(false);
        }
    }
}
