using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.SceneLoadingScreens.SplashScreen
{
    public interface ISplashScreen
    {
        UniTask ShowSplashAsync(CancellationToken ct);

        void NotifyFinish();

        void HideSplash();
    }
}
