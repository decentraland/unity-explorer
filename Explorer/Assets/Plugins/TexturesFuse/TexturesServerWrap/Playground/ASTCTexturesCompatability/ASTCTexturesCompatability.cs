using DCL.Utilities.Extensions;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground.ASTCTexturesCompatability
{
    public class ASTCTexturesCompatability : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer = null!;

        [Header("Config")]
        [SerializeField] private Options options = new ();
        [Space]
        [SerializeField] private string path = "Assets/Plugins/TexturesFuse/TexturesServerWrap/Playground/ASTCTexturesCompatability/test.astc";
        [SerializeField] private int width = 4000;
        [SerializeField] private int height = 6000;
        [SerializeField] private TextureFormat format = TextureFormat.ASTC_10x10;

        private byte[] buffer = Array.Empty<byte>();

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            meshRenderer.EnsureNotNull();

            buffer = File.ReadAllBytes(path);
            var texture = new Texture2D(width, height, format, false);
            texture.LoadRawTextureData(buffer);
            texture.Apply();

            Apply(texture);
        }

        private void Apply(Texture texture)
        {
            var material = meshRenderer.material!;
            material.mainTexture = texture;
            meshRenderer.material = material;

            meshRenderer.transform.localScale = new Vector3(
                options.BaseScale * ((float)texture.width / texture.height),
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
