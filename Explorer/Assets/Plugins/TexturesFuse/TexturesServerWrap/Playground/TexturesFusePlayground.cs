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
        [Space]
        [SerializeField] private string outputPath = "Assets/Plugins/TexturesFuse/TexturesServerWrap/Playground/ASTCTexturesCompatability/test_output.astc";

        private ITexturesUnzip unzip = null!;
        private byte[] buffer = Array.Empty<byte>();
        private OwnedTexture2D? texture;

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            meshRenderer.EnsureNotNull();

            unzip = new TexturesUnzip(options.InitOptions, options);
            buffer = File.ReadAllBytes(path);

            var result = FetchedAndOverrideTexture();
            Apply(result);
        }

        private void Update()
        {
            if (stressMode)
            {
                var result = FetchedAndOverrideTexture();
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

        private OwnedTexture2D FetchedAndOverrideTexture()
        {
            texture?.Dispose();
            texture = unzip.TextureFromBytes(buffer);
            return texture;
        }

        [ContextMenu(nameof(SaveToFile))]
        public void SaveToFile()
        {
            File.WriteAllBytes(outputPath, texture!.Texture.GetRawTextureData()!);
        }

        [Serializable]
        private class Options : ITexturesUnzip.IOptions
        {
            [SerializeField] private int maxSide = 1024;
            [SerializeField] private float baseScale = 8;
            [SerializeField] private Mode mode = Mode.RGB;
            [SerializeField] private NativeMethods.InitOptions initOptions;
            [SerializeField] private NativeMethods.Swizzle swizzle;

            public Mode Mode => mode;

            public NativeMethods.Swizzle Swizzle => swizzle;

            public int MaxSide => maxSide;

            public float BaseScale => baseScale;

            public NativeMethods.InitOptions InitOptions => initOptions;
        }
    }
}
