using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground
{
    public class NormalMapTexturesFusePlayground : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MeshRenderer cubeMesh = null!;
        [Header("Config")]
        [SerializeField] private TexturesFusePlayground.Options options = new ();
        [SerializeField] private string path = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image.jpg";
        [SerializeField] private string normalMapPath = "Assets/Plugins/TexturesFuse/textures-server/FreeImage/Source/FFI/image_normal.jpg";
        [SerializeField] private float rotationSpeed = 1;
        [SerializeField] private bool debugOutputFromNative;
        [Header("Debug")]
        [SerializeField] private Texture2D? currentTexture;
        [SerializeField] private Texture2D? currentNormalMapTexture;

        private OwnedTexture2D? texture;

        [ContextMenu(nameof(Start))]
        [SuppressMessage("ReSharper", "Unity.PreferAddressByIdToGraphicsParams")]
        private async void Start()
        {
            cubeMesh.EnsureNotNull();
            using var unzip = /*ITexturesUnzip.NewDebug();*/ new PooledTexturesUnzip(() => new TexturesUnzip(options.InitOptions, options, debugOutputFromNative), 2);
            byte[] buffer = await File.ReadAllBytesAsync(path, destroyCancellationToken)! ?? Array.Empty<byte>();
            byte[] normalBuffer = await File.ReadAllBytesAsync(normalMapPath, destroyCancellationToken)! ?? Array.Empty<byte>();

            var texture1 = (await unzip.TextureFromBytesAsync(buffer, destroyCancellationToken)).Unwrap();

            currentTexture = texture1.Texture;
            currentNormalMapTexture = NewNormalMapTexture(normalBuffer);

            var mpb = new MaterialPropertyBlock();
            mpb.SetTexture("_BaseMap", currentTexture);
            mpb.SetTexture("_BumpMap", currentNormalMapTexture);

            cubeMesh.SetPropertyBlock(mpb);

            while (this && destroyCancellationToken.IsCancellationRequested == false)
            {
                cubeMesh.transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
                await UniTask.Yield();
            }

            texture1.Dispose();
        }

        private Texture2D NewNormalMapTexture(byte[] buffer)
        {
            const int WIDTH = 512;
            const int HEIGHT = 512;

            var normalMapTexture = new Texture2D(WIDTH, HEIGHT, TextureFormat.BC5, false, true);

            normalMapTexture.LoadRawTextureData(buffer);
            normalMapTexture.Apply();

            return normalMapTexture;
        }
    }
}
