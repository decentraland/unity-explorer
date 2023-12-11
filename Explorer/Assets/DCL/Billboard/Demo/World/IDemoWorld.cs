using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Billboard.Demo.World
{
    public interface IDemoWorld
    {
        void Update();
    }

    public static class DemoWorldExtensions
    {
        public static async UniTask RunAsync(this IDemoWorld world, CancellationToken token)
        {
            while (token.IsCancellationRequested is false)
            {
                world.Update();
                await UniTask.Yield();
            }
        }
    }
}
