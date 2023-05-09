using UnityEngine;
using UnityEngine.Profiling;

namespace Global
{
    /// <summary>
    /// An entry point to install and resolve all dependencies
    /// </summary>
    public class EntryPoint : MonoBehaviour
    {
        public SceneSharedContainer SceneSharedContainer { get; private set; }

        private void Awake()
        {
            SceneSharedContainer = Install();
        }

        public static SceneSharedContainer Install()
        {
            Profiler.BeginSample($"{nameof(EntryPoint)}.Install");

            var componentsContainer = ComponentsContainer.Create();
            var sceneSharedContainer = SceneSharedContainer.Create(componentsContainer);

            Profiler.EndSample();
            return sceneSharedContainer;
        }
    }
}
