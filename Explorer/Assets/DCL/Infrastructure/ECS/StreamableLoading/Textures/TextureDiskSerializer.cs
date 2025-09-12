using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Threading;
using DCL.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Utility.Types;

namespace ECS.StreamableLoading.Textures
{
    public class TextureDiskSerializer : IDiskSerializer<Texture2DData, SerializeMemoryIterator<TextureDiskSerializer.State>>
    {
        public async UniTask<Texture2DData> DeserializeAsync(SlicedOwnedMemory<byte> data, CancellationToken token)
        {
            var meta = Meta.FromSpan(data.Memory.Span);
            
            await UniTask.SwitchToMainThread();
            var texture = new Texture2D(meta.width, meta.height, meta.format, meta.mipCount, meta.linear, true)
            {
                filterMode = meta.filterMode,
                wrapMode = meta.wrapMode,
                wrapModeU = meta.wrapModeU,
                wrapModeV = meta.wrapModeV,
                wrapModeW = meta.wrapModeW
            };
            
            using var handle = data.Memory.Pin();

            unsafe { texture.LoadRawTextureData((IntPtr)handle.Pointer + meta.ArrayLength, data.Memory.Length - meta.ArrayLength); }
            
            texture.Apply();

            // LoadRawTextureData copies the data
            data.Dispose();
            
            return new Texture2DData(texture);
        }

        public SerializeMemoryIterator<State> Serialize(Texture2DData data)
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
            public FilterMode filterMode;
            public TextureWrapMode wrapMode;
            public TextureWrapMode wrapModeU;
            public TextureWrapMode wrapModeV;
            public TextureWrapMode wrapModeW;

            public int ArrayLength => 16;

            public Meta(Texture2D texture2D) : this()
            {
                width = texture2D.width;
                height = texture2D.height;
                format = texture2D.format;
                mipCount = texture2D.mipmapCount;
                linear = GraphicsFormatUtility.IsSRGBFormat(texture2D.graphicsFormat) == false;
                filterMode = texture2D.filterMode;
                wrapMode = texture2D.wrapMode;
                wrapModeU = texture2D.wrapModeU;
                wrapModeV = texture2D.wrapModeV;
                wrapModeW = texture2D.wrapModeW;
            }

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
                span[11] = (byte)filterMode;
                span[12] = (byte)wrapMode;
                span[13] = (byte)wrapModeU;
                span[14] = (byte)wrapModeV;
                span[15] = (byte)wrapModeW;
            }

            public static Meta FromSpan(ReadOnlySpan<byte> array)
            {
                return new Meta
                {
                    width = array[0] | (array[1] << 8) | (array[2] << 16) | (array[3] << 24),
                    height = array[4] | (array[5] << 8) | (array[6] << 16) | (array[7] << 24),
                    format = (TextureFormat)array[8],
                    mipCount = array[9],
                    linear = array[10] == 1,
                    filterMode = (FilterMode)array[11],
                    wrapMode = (TextureWrapMode)array[12],
                    wrapModeU = (TextureWrapMode)array[13],
                    wrapModeV = (TextureWrapMode)array[14],
                    wrapModeW = (TextureWrapMode)array[15]
                };
            }
        }
    }
}
