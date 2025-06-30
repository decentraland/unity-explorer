using DCL.Optimization.PerformanceBudgeting;
using SceneRunner.Scene;

namespace SceneRunner
{
    public class PortableExperienceSceneFacade : SceneFacade
    {
        public PortableExperienceSceneFacade(
            ISceneData sceneData,
            SceneInstanceDependencies.WithRuntimeAndJsAPIBase deps,
            IPerformanceBudget performanceBudget
        ) : base(sceneData, deps, performanceBudget)
        {
            SetIsCurrent(true);
        }
    }
}
