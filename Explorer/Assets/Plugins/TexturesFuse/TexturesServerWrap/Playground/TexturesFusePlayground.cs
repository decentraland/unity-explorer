using DCL.Utilities.Extensions;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class TexturesFusePlayground : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer = null!;

        [Header("Config")]
        [SerializeField] private Options options = new ();
        [SerializeField] private string path = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            meshRenderer.EnsureNotNull();

            var unzip = new TexturesUnzip(options);
            byte[] bytes = File.ReadAllBytes(path);
            var result = unzip.TextureFromBytes(bytes);

            var material = meshRenderer.material!;
            material.mainTexture = result.Texture;
            meshRenderer.material = material;

            meshRenderer.transform.localScale = new Vector3(
                options.BaseScale * ((float)result.Texture.width / result.Texture.height),
                options.BaseScale,
                options.BaseScale
            );
        }

        [Serializable]
        private class Options : ITexturesUnzip.IOptions
        {
            [SerializeField] private int maxSide = 1024;
            [SerializeField] private float baseScale = 8;

            public int MaxSide => maxSide;

            public float BaseScale => baseScale;
        }
    }
}
