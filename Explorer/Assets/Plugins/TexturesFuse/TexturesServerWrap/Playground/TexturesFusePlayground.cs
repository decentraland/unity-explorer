using DCL.Utilities.Extensions;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.IO;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class TexturesFusePlayground : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer = null!;
        [SerializeField] private float baseScale = 8;

        [Header("Config")]
        [SerializeField]
        private string path = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";

        private void Start()
        {
            meshRenderer.EnsureNotNull();

            var unzip = new TexturesUnzip();
            byte[] bytes = File.ReadAllBytes(path);
            var result = unzip.TextureFromBytes(bytes);

            var material = meshRenderer.material!;
            material.mainTexture = result.Texture;
            meshRenderer.material = material;

            meshRenderer.transform.localScale = new Vector3(
                baseScale * ((float)result.Texture.width / result.Texture.height),
                baseScale,
                baseScale
            );
        }
    }
}
