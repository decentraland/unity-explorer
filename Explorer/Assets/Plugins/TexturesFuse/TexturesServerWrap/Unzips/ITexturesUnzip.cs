using System;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public interface ITexturesUnzip
    {
        (Texture2D texture, IDisposable memoryOwner) TextureFromBytes(ReadOnlySpan<byte> bytes);
    }
}
