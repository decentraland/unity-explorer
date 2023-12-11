using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Billboard.DemoTest.World
{
    public interface IDemoWorld
    {
        void Update();
    }

    public static class DemoWorldExtensions
    {
        public static async UniTask Run(this IDemoWorld world, CancellationToken token)
        {
            while (token.IsCancellationRequested is false)
            {
                world.Update();
                await UniTask.Yield();
            }
        }
    }
}
