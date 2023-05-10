using Cysharp.Threading.Tasks;
using System.Threading;

namespace SceneRunner.Scene
{
    public interface ISceneFactory
    {
        /// <summary>
        /// Must be started on the main thread.
        /// Starts scripts downloading on the main thread because of UnityWebRequest limitations
        /// Then switches to the background thread for the rest of instantiations
        /// </summary>
        /// <param name="jsCodeUrl"></param>
        /// <param name="ct"></param>
        /// <returns>Scene Facade on the background thread</returns>
        UniTask<ISceneFacade> CreateScene(string jsCodeUrl, CancellationToken ct);
    }
}
