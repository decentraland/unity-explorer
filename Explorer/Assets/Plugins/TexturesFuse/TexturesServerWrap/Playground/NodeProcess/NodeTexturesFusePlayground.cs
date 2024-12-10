using Cysharp.Threading.Tasks;
using DCL.Platforms;
using Plugins.TexturesFuse.TexturesServerWrap.CompressShaders;
using Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground.NodeProcess
{
    public class NodeTexturesFusePlayground : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private AbstractDebugDisplay display = null!;

        [SerializeField]
        private string pathOrUri = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";

        [SerializeField]
        private NodeTexturesFuse.InputArgs inputArgs;
        [Header("Debug")]
        [SerializeField]
        private NodeTexturesFuse.OutputResult outputResult;

        private ITexturesFuse? texturesFuse;
        private IOwnedTexture2D? ownedTexture;

        [ContextMenu(nameof(Start))]
        private void Start()
        {
            StartAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            ownedTexture?.Dispose();
            texturesFuse?.Dispose();
        }

        private async UniTaskVoid StartAsync(CancellationToken token)
        {
            await ICompressShaders
                 .NewDefault(() => new NodeTexturesFuse(inputArgs), new Platform())
                 .WarmUpIfRequiredAsync(token);

            texturesFuse ??= new NodeTexturesFuse(inputArgs);

            byte[] data = await File.ReadAllBytesAsync(pathOrUri, token)!;
            var result = await texturesFuse.TextureFromBytesAsync(data, TextureType.Albedo, token);

            if (result.Success)
            {
                ownedTexture?.Dispose();
                ownedTexture = result.Value;

                display.Display(ownedTexture.Texture);
            }
        }
    }
}
