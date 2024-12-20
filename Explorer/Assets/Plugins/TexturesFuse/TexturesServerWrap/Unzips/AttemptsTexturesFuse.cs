using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System.Threading;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    // Safety-net, should be removed later
    public class AttemptsTexturesFuse : ITexturesFuse
    {
        private readonly ITexturesFuse origin;
        private readonly int attempts;

        public AttemptsTexturesFuse(ITexturesFuse origin, int attempts = 3)
        {
            this.origin = origin;
            this.attempts = attempts;
        }

        public void Dispose()
        {
            origin.Dispose();
        }

        public async UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(ITexturesFuse.ImageData imageData, CancellationToken token)
        {
            EnumResult<IOwnedTexture2D, NativeMethods.ImageResult> result = default;

            for (var i = 0; i < attempts; i++)
            {
                result = await origin.TextureFromBytesAsync(imageData, token);

                if (result.Success)
                    break;

                ReportHub.LogWarning(ReportCategory.TEXTURES, "TextureFromBytesAsync attempt failed, retrying...");
            }

            return result;
        }
    }
}
