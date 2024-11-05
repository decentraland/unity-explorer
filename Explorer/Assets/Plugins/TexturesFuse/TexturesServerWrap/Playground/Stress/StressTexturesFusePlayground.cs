using Cysharp.Threading.Tasks;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class StressTexturesFusePlayground : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private TexturesFusePlayground.Options options = new ();
        [Space]
        [SerializeField] private TextureType textureType = TextureType.Albedo;
        [SerializeField] private string path = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";
        [Space]
        [Min(1)]
        [SerializeField] private int workersCount = 1;
        [SerializeField] private float delay = 0.1f;
        [SerializeField] private bool debugOutputFromNative;

        [ContextMenu(nameof(Start))]
        private IEnumerator Start()
        {
            byte[] buffer = File.ReadAllBytes(path);

            var unzip = ITexturesUnzip.NewDefault(options, workersCount);
            var wait = new WaitForSeconds(delay);

            while (this)
            {
                AcquireAndDisposeAsync(buffer, unzip).Forget();
                yield return wait;
            }
        }

        private async UniTaskVoid AcquireAndDisposeAsync(byte[] buffer, ITexturesUnzip unzip)
        {
            var result = await unzip.TextureFromBytesAsync(buffer, textureType, destroyCancellationToken);

            if (result.Success)
                result.Value.Dispose();
        }
    }
}
