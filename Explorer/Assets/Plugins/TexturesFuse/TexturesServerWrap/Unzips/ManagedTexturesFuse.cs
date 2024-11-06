using Cysharp.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    /// <summary>
    ///     For testing purposes only
    /// </summary>
    public class ManagedTexturesFuse : ITexturesFuse
    {
        public void Dispose()
        {
            //ignore
        }

        public async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(IntPtr bytes, int bytesLength, TextureType type, CancellationToken token)
        {
            var texture = new Texture2D(1, 1);
            var array = new byte[bytesLength];
            Marshal.Copy(bytes, array, 0, bytesLength);

            return texture.LoadImage(array)
                ? EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>.SuccessResult(new IOwnedTexture2D.Const(texture))
                : EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>.ErrorResult(NativeMethods.ImageResult.ErrorUnknown, "Failed to load image");
        }
    }
}
