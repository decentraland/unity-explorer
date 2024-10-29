using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class TexturesFusePlayground : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private AbstractDebugDisplay display = null!;
        [Header("Config")]
        [SerializeField] private Options options = new ();
        [SerializeField] private string path = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";
        [SerializeField] private bool debugOutputFromNative;
        [Space]
        [SerializeField] private string outputPath = "Assets/Plugins/TexturesFuse/TexturesServerWrap/Playground/ASTCTexturesCompatability/test_output.astc";

        private ITexturesUnzip unzip = null!;
        private byte[] buffer = Array.Empty<byte>();
        private IOwnedTexture2D? texture;

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            display.EnsureNotNull();
            unzip = new TexturesUnzip(options.InitOptions, options, debugOutputFromNative);
            buffer = await File.ReadAllBytesAsync(path, destroyCancellationToken)!;
            print($"Original size: {buffer.Length} bytes");

            var result = await FetchedAndOverrideTextureAsync();
            display.Display(result.Texture);
        }

        private async UniTask<IOwnedTexture2D> FetchedAndOverrideTextureAsync()
        {
            texture?.Dispose();
            texture = (await unzip.TextureFromBytesAsync(buffer, TextureType.Albedo, destroyCancellationToken)).Unwrap();
            print($"Compressed size: {texture.Texture.GetRawTextureData()!.Length} bytes");
            return texture;
        }

        [ContextMenu(nameof(SaveToFile))]
        public void SaveToFile()
        {
            File.WriteAllBytes(outputPath, texture!.Texture.GetRawTextureData()!);
        }

        [Serializable]
        public class Options : ITexturesUnzip.IOptions
        {
            [SerializeField] private int maxSide = 1024;

            [SerializeField] private NativeMethods.Adjustments adjustments;
            [SerializeField] private Mode mode = Mode.RGB;
            [SerializeField] private NativeMethods.InitOptions initOptions;
            [SerializeField] private NativeMethods.Swizzle swizzle;

            public Mode Mode => mode;

            public NativeMethods.Swizzle Swizzle => swizzle;

            public int MaxSide => maxSide;

            public NativeMethods.Adjustments Adjustments => adjustments;

            public NativeMethods.InitOptions InitOptions => initOptions;
        }
    }
}
