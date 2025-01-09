using Cysharp.Threading.Tasks;
using Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class RGBATexturesFusePlayground : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private AbstractDebugDisplay display = null!;
        [SerializeField] private string displayRGBAPath = string.Empty;
        [SerializeField] private int width = 512, height = 512;

        [Header("Config")]
        [SerializeField] private TexturesFusePlayground.Options options = new ();
        [SerializeField] private string path = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";
        [SerializeField] private bool debugOutputFromNative;
        [Space]
        [SerializeField] private string outputPath = "Assets/Plugins/TexturesFuse/TexturesServerWrap/Playground/ASTCTexturesCompatability/test_output.astc";

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            var unzip = new Unzips.TexturesFuse(options.InitOptions, options, debugOutputFromNative);
            byte[] buffer = await File.ReadAllBytesAsync(path, destroyCancellationToken)!;
            print($"Original size: {buffer.Length} bytes");

            await using var outputStream = new FileStream(outputPath, FileMode.Create);

            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    var result = unzip.LoadRGBAImage(
                        ptr,
                        buffer.Length,
                        out byte* output,
                        out int outputLength,
                        out uint width,
                        out uint height,
                        out TextureFormat format,
                        out IntPtr handle
                    );

                    outputStream.Write(new Span<byte>(output, outputLength));
                    print($"Output size: {outputLength} bytes, width: {width}, height: {height}, format: {format}, result: {result}");
                }
            }

            if (File.Exists(displayRGBAPath))
            {
                byte[] data = await File.ReadAllBytesAsync(displayRGBAPath)!;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                texture.LoadRawTextureData(data);
                texture.Apply();
                display.Display(texture);
            }
            else
                print($"Display file not found: {displayRGBAPath}");
        }
    }
}
