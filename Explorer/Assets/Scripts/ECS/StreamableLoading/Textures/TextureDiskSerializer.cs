using Cysharp.Threading.Tasks;
using DCL.Caches.Disk;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ECS.StreamableLoading.Textures
{
    public class TextureDiskSerializer : IDiskSerializer<Texture2DData>
    {
        public async UniTask<byte[]> Serialize(Texture2DData data, CancellationToken token)
        {
            //TODO must be optimised to avoid extra allocations
            await UniTask.SwitchToMainThread();
            byte[] textureData = data.Asset.GetRawTextureData()!;

            var meta = new Meta
            {
                width = data.Asset.width,
                height = data.Asset.height,
                format = data.Asset.format,
                mipCount = data.Asset.mipmapCount,
                linear = GraphicsFormatUtility.IsSRGBFormat(data.Asset.graphicsFormat) == false,
            };

            byte[] metaData = meta.ToArray();

            var result = new byte[metaData.Length + textureData.Length];

            Buffer.BlockCopy(metaData, 0, result, 0, metaData.Length);
            Buffer.BlockCopy(textureData, 0, result, metaData.Length, textureData.Length);

            return result;
        }

        public UniTask<Texture2DData> Deserialize(byte[] data, CancellationToken token)
        {
            //Read from the start
            var meta = Meta.FromArray(data);
            var texture = new Texture2D(meta.width, meta.height, meta.format, meta.mipCount, meta.linear, true);
            var trimmedData = new byte[data.Length - meta.ArrayLength];
            Buffer.BlockCopy(data, meta.ArrayLength, trimmedData, 0, trimmedData.Length);
            texture.SetPixelData(trimmedData, 0);
            texture.Apply();

            return UniTask.FromResult(new Texture2DData(texture));
        }

        [Serializable]
        public struct Meta
        {
            public int width;
            public int height;
            public TextureFormat format;
            public int mipCount;
            public bool linear;

            public byte[] ToArray()
            {
                return new[]
                {
                    (byte)(width & 0xFF),
                    (byte)((width >> 8) & 0xFF),
                    (byte)((width >> 16) & 0xFF),
                    (byte)((width >> 24) & 0xFF),
                    (byte)(height & 0xFF),
                    (byte)((height >> 8) & 0xFF),
                    (byte)((height >> 16) & 0xFF),
                    (byte)((height >> 24) & 0xFF),
                    (byte)format,
                    (byte)mipCount,
                    (byte)(linear ? 1 : 0),
                };
            }

            public int ArrayLength => 11;

            public static Meta FromArray(byte[] array) =>
                new ()
                {
                    width = array[0] | (array[1] << 8) | (array[2] << 16) | (array[3] << 24),
                    height = array[4] | (array[5] << 8) | (array[6] << 16) | (array[7] << 24),
                    format = (TextureFormat)array[8],
                    mipCount = array[9],
                    linear = array[10] == 1
                };
        }
    }
}
