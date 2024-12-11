using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    internal class FallbackTexturesFuse : ITexturesFuse
    {
        private readonly ITexturesFuse origin;
        private readonly ITexturesFuse fallback;

        public FallbackTexturesFuse(ITexturesFuse origin) : this(origin, new ManagedTexturesFuse()) { }

        public FallbackTexturesFuse(ITexturesFuse origin, ITexturesFuse fallback)
        {
            this.origin = origin;
            this.fallback = fallback;
        }

        public async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(IntPtr bytes, int bytesLength, TextureType type, CancellationToken token)
        {
            var result = await origin.TextureFromBytesAsync(bytes, bytesLength, type, token);

            if (result.Success == false && result.Error!.Value.State is NativeMethods.ImageResult.ErrorUnknown)
            {
                ReportHub.LogError(ReportCategory.TEXTURES, $"ErrorUnknown occured during the main compression: {result.Error.Value.Message}");
                result = await fallback.TextureFromBytesAsync(bytes, bytesLength, type, token);
            }

            return result;
        }

        public void Dispose()
        {
            origin.Dispose();
            fallback.Dispose();
        }
    }
}
