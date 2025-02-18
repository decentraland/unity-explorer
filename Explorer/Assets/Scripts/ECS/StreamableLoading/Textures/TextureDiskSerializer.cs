using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Buffers;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.Textures
{
    public class TextureDiskSerializer : IDiskSerializer<Texture2DData>
    {
        public SlicedOwnedMemory<byte> Serialize(Texture2DData data) =>
            ToArray(data);

        public async UniTask<Texture2DData> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            var meta = Meta.FromSpan(data.Memory.Span);

            await UniTask.SwitchToMainThread();
            var texture = new Texture2D(meta.width, meta.height, meta.format, meta.mipCount, meta.linear, true);

            using var handle = data.Memory.Pin();

            unsafe { texture.LoadRawTextureData((IntPtr)handle.Pointer + meta.ArrayLength, data.Memory.Length - meta.ArrayLength); }

            texture.Apply();
            return new Texture2DData(new MemoryOwnedTexture2D(data, texture));
        }

        private static SlicedOwnedMemory<byte> ToArray(Texture2DData data)
        {
            var textureData = data.Asset.GetRawTextureData<byte>()!;

            var meta = new Meta(data.Asset);
            Span<byte> metaData = stackalloc byte[meta.ArrayLength];
            meta.ToSpan(metaData);

            int targetSize = metaData.Length + textureData.Length;
            var memoryOwner = new SlicedOwnedMemory<byte>(MemoryPool<byte>.Shared!.Rent(targetSize)!, targetSize);
            var memory = memoryOwner.Memory;

            metaData.CopyTo(memory.Span);
            textureData.AsSpan().CopyTo(memory.Slice(metaData.Length).Span);

            return memoryOwner;
        }

        [Serializable]
        public struct Meta
        {
            public int width;
            public int height;
            public TextureFormat format;
            public int mipCount;
            public bool linear;

            public Meta(Texture2D texture2D) : this()
            {
                width = texture2D.width;
                height = texture2D.height;
                format = texture2D.format;
                mipCount = texture2D.mipmapCount;
                linear = GraphicsFormatUtility.IsSRGBFormat(texture2D.graphicsFormat) == false;
            }

            public int ArrayLength => 11;

            /// <param name="span">Span with size of ArrayLength</param>
            public void ToSpan(Span<byte> span)
            {
                span[0] = (byte)(width & 0xFF);
                span[1] = (byte)((width >> 8) & 0xFF);
                span[2] = (byte)((width >> 16) & 0xFF);
                span[3] = (byte)((width >> 24) & 0xFF);
                span[4] = (byte)(height & 0xFF);
                span[5] = (byte)((height >> 8) & 0xFF);
                span[6] = (byte)((height >> 16) & 0xFF);
                span[7] = (byte)((height >> 24) & 0xFF);
                span[8] = (byte)format;
                span[9] = (byte)mipCount;
                span[10] = (byte)(linear ? 1 : 0);
            }

            public static Meta FromSpan(ReadOnlySpan<byte> array) =>
                new ()
                {
                    width = array[0] | (array[1] << 8) | (array[2] << 16) | (array[3] << 24),
                    height = array[4] | (array[5] << 8) | (array[6] << 16) | (array[7] << 24),
                    format = (TextureFormat)array[8],
                    mipCount = array[9],
                    linear = array[10] == 1,
                };
        }

        private class MemoryOwnedTexture2D : IOwnedTexture2D
        {
            private readonly SlicedOwnedMemory<byte> memoryOwner;

            public Texture2D Texture { get; }

            public MemoryOwnedTexture2D(SlicedOwnedMemory<byte> memoryOwner, Texture2D texture)
            {
                this.memoryOwner = memoryOwner;
                Texture = texture;
            }

            public void Dispose()
            {
                memoryOwner.Dispose();
                Object.Destroy(Texture);
            }
        }
    }
}
