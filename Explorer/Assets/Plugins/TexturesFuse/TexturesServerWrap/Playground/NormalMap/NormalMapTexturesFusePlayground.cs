using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEngine;

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

            var results = await UniTask.WhenAll(
                unzip.TextureFromBytesAsync(buffer, destroyCancellationToken),
                unzip.TextureFromBytesAsync(normalBuffer, destroyCancellationToken)
            );

            var texture1 = results.Item1.Unwrap();
            var texture2 = results.Item2.Unwrap();

            currentTexture = texture1.Texture;
            currentNormalMapTexture = NewNormalMapTexture(texture2.Texture);

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
            texture2.Dispose();
        }

        private static Texture2D NewNormalMapTexture(Texture2D originalTexture)
        {
            var output = new Texture2D(originalTexture.width, originalTexture.height, TextureFormat.RGB24, false);

            for (var i = 0; i < originalTexture.width; i++)
            for (var j = 0; j < originalTexture.height; j++)
            {
                var pixel = originalTexture.GetPixel(i, j);
                float tempX = (pixel.r * 2) - 1;
                float tempY = -(pixel.g * 2) - 1;
                float tempZ = Mathf.Sqrt(1 - (tempX * tempX) - (tempY * tempY));

                output.SetPixel(
                    i,
                    j,
                    new Color((tempX * 0.5f) + 0.5f, (tempY * 0.5f) + 0.5f, (tempZ * 0.5f) + 0.5f)
                );
            }

            output.Apply();

            return output;
        }
    }
}
