using Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class AdjustGammaTexturesPlayground : MonoBehaviour
    {
        [SerializeField] private Bundle left = new ();
        [SerializeField] private Bundle right = new ();

        [Space]
        [SerializeField] private string path = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";
        [SerializeField] private bool debugOutputFromNative;
        [Space]
        [SerializeField] private string outputPath = "Assets/Plugins/TexturesFuse/TexturesServerWrap/Playground/ASTCTexturesCompatability/test_output.astc";

        private byte[] buffer = Array.Empty<byte>();
        private OwnedTexture2D? texture;

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            buffer = File.ReadAllBytes(path);
            left.Apply(buffer, debugOutputFromNative);
            right.Apply(buffer, debugOutputFromNative);
        }

        [ContextMenu(nameof(SaveToFile))]
        public void SaveToFile()
        {
            File.WriteAllBytes(outputPath, texture!.Texture.GetRawTextureData()!);
        }

        [Serializable]
        private class Bundle
        {
            [SerializeField] private AbstractDebugDisplay display = null!;
            [SerializeField] private TexturesFusePlayground.Options options = new ();
            private ITexturesUnzip unzip = null!;

            public void Apply(byte[] imageData, bool debugOutputFromNative)
            {
                unzip = new TexturesUnzip(options.InitOptions, options, debugOutputFromNative);
                var result = unzip.TextureFromBytes(imageData);
                display.Display(result.Texture);
            }
        }
    }
}
