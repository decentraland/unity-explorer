using DCL.Utilities.Extensions;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Frames
{
    public class MeshFrame : AbstractFrame
    {
        [SerializeField] private UnityEngine.Renderer renderer = null!;
        [SerializeField] private Material defaultMaterial = null!;
        [SerializeField] private int placeIndex;

        [Header("Status")]
        [SerializeField] private GameObject loadingStatus = null!;
        [SerializeField] private GameObject failedStatus = null!;

        [Header("Backplate")]
        [SerializeField] private int[] backplateIndexes = Array.Empty<int>();
        [SerializeField] private string backplateColorPropertyName = "_Color";

        private int backplateColorPropertyId;
        private MaterialPropertyBlock backplateMaterialPropertyBlock = null!;

        public void Awake()
        {
            renderer.EnsureNotNull();
            defaultMaterial.EnsureNotNull();
            loadingStatus.EnsureNotNull();
            failedStatus.EnsureNotNull();
            backplateMaterialPropertyBlock = new MaterialPropertyBlock();
            backplateColorPropertyId = Shader.PropertyToID(backplateColorPropertyName);

            HideStatuses();
        }

        public override void Paint(Color color)
        {
            backplateMaterialPropertyBlock.SetColor(backplateColorPropertyId, color);

            foreach (int backplateIndex in backplateIndexes)
                renderer.SetPropertyBlock(backplateMaterialPropertyBlock, backplateIndex);
        }

        public override void Place(Material picture)
        {
            renderer.materials![placeIndex] = picture;
            HideStatuses();
        }

        public override void UpdateStatus(Status status)
        {
            HideStatuses();

            var current = status switch
                          {
                              Status.Loading => loadingStatus,
                              Status.Failed => failedStatus,
                              _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
                          };

            current.SetActive(true);
        }

        private void HideStatuses()
        {
            loadingStatus.SetActive(false);
            failedStatus.SetActive(false);
        }
    }
}
