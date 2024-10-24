using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class StressTexturesFusePlayground : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private TexturesFusePlayground.Options options = new ();
        [SerializeField] private string path = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";
        [SerializeField] private int threads = 20;
        [SerializeField] private bool debugOutputFromNative;

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            byte[] buffer = File.ReadAllBytes(path);

            for (var i = 0; i < threads; i++)
                new Thread(
                    () =>
                    {
                        var unzip = new TexturesUnzip(options.InitOptions, options, debugOutputFromNative);

                        while (destroyCancellationToken.IsCancellationRequested == false)
                            unsafe
                            {
                                fixed (byte* ptr = buffer)
                                {
                                    // unzip.LoadASTCImage(ptr, buffer.Length, out _, out _, out _, out _, out var handle);
                                    // NativeMethods.TexturesFuseDispose(handle);
                                }
                            }
                    }
                ).Start();
        }
    }
}
