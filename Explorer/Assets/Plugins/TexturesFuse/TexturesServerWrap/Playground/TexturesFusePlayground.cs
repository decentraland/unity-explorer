using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class TexturesFusePlayground : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private AbstractDebugDisplay display = null!;
        [Header("Config")]
        [SerializeField] private TextureType textureType;
        [Space]
        [SerializeField] private bool overrideDefaultUnzip;
        [SerializeField] private Options options = new ();
        [FormerlySerializedAs("path")] [SerializeField] private string pathOrUri = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";
        [SerializeField] private bool debugOutputFromNative;
        [Space]
        [SerializeField] private string outputPath = "Assets/Plugins/TexturesFuse/TexturesServerWrap/Playground/ASTCTexturesCompatability/test_output.astc";

        private ITexturesFuse fuse = null!;
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

            fuse = overrideDefaultUnzip
                ? new Unzips.TexturesFuse(options.InitOptions, options, debugOutputFromNative).WithLog(string.Empty)
                : ITexturesFuse.NewDefault();

            buffer = await BufferAsync(pathOrUri);
            print($"Original size: {buffer.Length} bytes");

            var result = await FetchedAndOverrideTextureAsync();
            display.Display(result.Texture);
        }

        private async UniTask<IOwnedTexture2D> FetchedAndOverrideTextureAsync()
        {
            texture?.Dispose();
            texture = (await fuse.TextureFromBytesAsync(buffer, textureType, destroyCancellationToken)).Unwrap();
            print($"Compressed size: {texture.Texture.GetRawTextureData()!.Length} bytes");
            return texture;
        }

        private void OnDestroy()
        {
            texture?.Dispose();
            fuse.Dispose();
        }

        [ContextMenu(nameof(SaveToFile))]
        public void SaveToFile()
        {
            File.WriteAllBytes(outputPath, texture!.Texture.GetRawTextureData()!);
        }

        private async UniTask<byte[]> BufferAsync(string uri)
        {
            if (uri.StartsWith("http", StringComparison.Ordinal))
            {
                var result = await UnityWebRequest.Get(uri)!.SendWebRequest()!;

                if (result.result is not UnityWebRequest.Result.Success)
                    throw new Exception($"Failed to fetch {uri}");

                return result.downloadHandler!.data!;
            }

            return await File.ReadAllBytesAsync(pathOrUri, destroyCancellationToken)!;
        }

        [Serializable]
        public class Options : ITexturesFuse.IOptions
        {
            [SerializeField] private int maxSide = 1024;

            [SerializeField] private NativeMethods.Adjustments adjustments;
            [SerializeField] private Mode mode = Mode.RGB;
            [SerializeField] private NativeMethods.InitOptions initOptions;
            [SerializeField] private NativeMethods.Swizzle swizzle;
            [Header("CMP options")]
            [SerializeField] private bool useOverride;
            [Space]
            [SerializeField] private float fquality = 1;
            [SerializeField] private bool disableMultithreading = true;
            [SerializeField] private int dwnumThreads = 1;
            [SerializeField] private NativeMethods.CMP_Compute_type cmpComputeTypeEncode = NativeMethods.CMP_Compute_type.CMP_CPU;

            public Mode Mode => mode;

            public NativeMethods.Swizzle Swizzle => swizzle;

            public int MaxSide => maxSide;

            public NativeMethods.Adjustments Adjustments => adjustments;

            public NativeMethods.InitOptions InitOptions => initOptions;

            public NativeMethods.CMP_CompressOptions CMP_CompressOptions
            {
                get
                {
                    NativeMethods.CMP_CompressOptions defaultOptions = NativeMethods.CMP_CompressOptions.NewDefault();

                    if (useOverride)
                    {
                        defaultOptions.fquality = fquality;
                        defaultOptions.bDisableMultiThreading = disableMultithreading;
                        defaultOptions.dwnumThreads = (uint)dwnumThreads;
                        defaultOptions.nEncodeWith = cmpComputeTypeEncode;
                    }

                    return defaultOptions;
                }
            }
        }
    }
}
