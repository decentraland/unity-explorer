using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.DemoWorlds
{
    public interface IDemoWorld
    {
        /// <summary>
        /// Should be first setup, introduced this method to get rid off null check in update
        /// </summary>
        void SetUp();

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

        public static UniTask SetUpAndRunAsync(this IDemoWorld world, CancellationToken token)
        {
            world.SetUp();
            return world.RunAsync(token);
        }
    }
}
