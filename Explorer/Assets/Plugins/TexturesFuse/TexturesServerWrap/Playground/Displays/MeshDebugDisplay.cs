using DCL.Utilities.Extensions;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays
{
    public class MeshDebugDisplay : AbstractDebugDisplay
    {
        [SerializeField] private MeshRenderer meshRenderer = null!;
        [SerializeField] private float baseScale = 8;

        private void Start()
        {
            meshRenderer.EnsureNotNull();
        }

        public override void Display(Texture2D texture)
        {
            var material = meshRenderer.material!;
            material.mainTexture = texture;
            meshRenderer.material = material;

            meshRenderer.transform.localScale = new Vector3(
                baseScale * ((float)texture.width / texture.height),
                baseScale,
                baseScale
            );
        }
    }
}
