using Cysharp.Threading.Tasks;
using DCL.UI;
using System.Threading;
using Utility;

namespace DCL.UI
{
    public class ImageControllerProvider
    {
        private readonly UITextureProvider textureProvider;

        public ImageControllerProvider(UITextureProvider textureProvider)
        {
            this.textureProvider = textureProvider;
        }

        public ImageController Create(ImageView view)
        {
            return new ImageController(view, textureProvider);
        }

        public UniTask<Texture2DRef?> LoadTextureAsync(string url, CancellationToken ct)
        {
            return textureProvider.LoadTextureAsync(url, ct);
        }
    }
}
