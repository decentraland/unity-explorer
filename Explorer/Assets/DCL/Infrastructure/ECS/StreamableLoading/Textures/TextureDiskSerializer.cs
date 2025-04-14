using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ECS.StreamableLoading.Textures
{
    public class TextureDiskSerializer : IDiskSerializer<Texture2DData, SerializeMemoryIterator<TextureDiskSerializer.State>>
    {
        public SerializeMemoryIterator<State> Serialize(Texture2DData data) =>
            ToArray(data);

        public async UniTask<Texture2DData> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            var meta = Meta.FromSpan(data.Memory.Span);

            await UniTask.SwitchToMainThread();
            var texture = new Texture2D(meta.width, meta.height, meta.format, meta.mipCount, meta.linear, true);

            using var handle = data.Memory.Pin();

            unsafe { texture.LoadRawTextureData((IntPtr)handle.Pointer + meta.ArrayLength, data.Memory.Length - meta.ArrayLength); }

            texture.Apply();

            // LoadRawTextureData copies the data
            data.Dispose();

            return new Texture2DData(texture);
        }

        private static SerializeMemoryIterator<State> ToArray(Texture2DData data)
        {
            var textureData = data.Asset.GetRawTextureData<byte>()!;

            var meta = new Meta(data.Asset);
            State state = new State(meta, textureData);

            return SerializeMemoryIterator<State>.New(
                state,
                static (source, index, buffer) =>
                {
                    if (index == 0)
                    {
                        source.Meta.ToSpan(buffer.Span);
                        return source.Meta.ArrayLength;
                    }

                    // Address meta offset
                    index -= 1;

                    var span = source.TextureData.AsSpan();
                    return SerializeMemoryIterator.ReadNextData(index, span, buffer);
                },
                static (source, index, bufferLength) =>
                {
                    if (index == 0)
                        return true;

                    // Address meta offset
                    index -= 1;

                    return SerializeMemoryIterator.CanReadNextData(index, source.TextureData.Length, bufferLength);
                }
            );
        }

        public struct State
        {
            public readonly Meta Meta;
            public readonly NativeArray<byte> TextureData;

            public State(Meta meta, NativeArray<byte> textureData)
            {
                Meta = meta;
                TextureData = textureData;
            }
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
            public readonly void ToSpan(Span<byte> span)
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
    }
}
