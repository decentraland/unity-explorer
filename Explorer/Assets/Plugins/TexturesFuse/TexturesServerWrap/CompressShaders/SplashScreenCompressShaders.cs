using Cysharp.Threading.Tasks;
using DCL.SceneLoadingScreens.SplashScreen;
using System.Threading;

namespace Plugins.TexturesFuse.TexturesServerWrap.CompressShaders
{
    public class SplashScreenCompressShaders : ICompressShaders
    {
        private readonly ICompressShaders origin;
        private readonly ISplashScreen splashScreen;

        public SplashScreenCompressShaders(ICompressShaders origin, ISplashScreen splashScreen)
        {
            this.origin = origin;
            this.splashScreen = splashScreen;
        }

        public bool AreReady() =>
            origin.AreReady();

        public async UniTask WarmUpAsync(CancellationToken token)
        {
            if (AreReady())
                return;

            using (splashScreen.ShowWithContext("Compiling shaders\nIt performs once..."))
                await origin.WarmUpAsync(token);
        }
    }
}
