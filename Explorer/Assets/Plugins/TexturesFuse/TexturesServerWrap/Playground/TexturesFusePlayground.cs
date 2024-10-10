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
        [SerializeField] private bool stressMode;

        private ITexturesUnzip unzip = null!;
        private byte[] buffer = Array.Empty<byte>();

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            meshRenderer.EnsureNotNull();

            unzip = new TexturesUnzip(options.InitOptions, options);
            buffer = File.ReadAllBytes(path);

            var result = unzip.TextureFromBytes(buffer);
            Apply(result);
        }

        private void Update()
        {
            if (stressMode)
            {
                var result = unzip.TextureFromBytes(buffer);
                Apply(result);
            }
        }

        private void Apply(OwnedTexture2D result)
        {
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
            [SerializeField] private ITexturesUnzip.Mode mode = ITexturesUnzip.Mode.RGB;
            [SerializeField] private NativeMethods.InitOptions initOptions;

            public ITexturesUnzip.Mode Mode => mode;

            public int MaxSide => maxSide;

            public float BaseScale => baseScale;

            public NativeMethods.InitOptions InitOptions => initOptions;
        }
    }
}
