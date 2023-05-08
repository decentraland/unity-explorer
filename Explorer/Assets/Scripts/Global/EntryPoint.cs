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
            Profiler.BeginSample($"{nameof(EntryPoint)}.Install");

            var componentsContainer = ComponentsContainer.Create();
            SceneSharedContainer = SceneSharedContainer.Create(componentsContainer);

            Profiler.EndSample();
        }
    }
}
