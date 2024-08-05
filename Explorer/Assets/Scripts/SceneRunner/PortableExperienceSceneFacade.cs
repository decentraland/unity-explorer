using SceneRunner.Scene;
using UnityEngine;

namespace SceneRunner
{
    public class PortableExperienceSceneFacade : SceneFacade
    {
        public PortableExperienceSceneFacade(
            ISceneData sceneData,
            SceneInstanceDependencies.WithRuntimeAndJsAPIBase deps) : base(sceneData, deps)
        {
            SetIsCurrent(true);
        }

        public new bool Contains(Vector2Int parcel) => true;
    }
}
