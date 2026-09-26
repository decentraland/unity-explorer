using SceneRunner.Scene;

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
    }
}
