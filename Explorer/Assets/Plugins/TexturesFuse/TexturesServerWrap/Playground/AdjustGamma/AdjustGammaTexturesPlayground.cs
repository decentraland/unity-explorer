using Cysharp.Threading.Tasks;
using Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using System.Threading;
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
            left.ApplyAsync(buffer, debugOutputFromNative, destroyCancellationToken).Forget();
            right.ApplyAsync(buffer, debugOutputFromNative, destroyCancellationToken).Forget();
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
            private ITexturesFuse fuse = null!;

            public async UniTaskVoid ApplyAsync(byte[] imageData, bool debugOutputFromNative, CancellationToken token)
            {
                fuse = new Unzips.TexturesFuse(options.InitOptions, options, debugOutputFromNative);
                var result = await fuse.TextureFromBytesAsync(imageData, TextureType.Albedo, token);
                display.Display(result.Unwrap().Texture);
            }
        }
    }
}
