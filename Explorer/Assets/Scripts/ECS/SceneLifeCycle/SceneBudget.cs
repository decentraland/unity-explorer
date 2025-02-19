using System.Collections.Generic;
using DCL.Optimization.PerformanceBudgeting;

namespace ECS.SceneLifeCycle
{
    public class SceneBudget : IPerformanceBudget
    {

        private readonly HashSet<string> loadingScenes = new HashSet<string>();
        private readonly HashSet<string> loadedScenes = new HashSet<string>();
        private readonly HashSet<string> unloadingScenes = new HashSet<string>();
        public double OverridenUnloadingSqrDistance { get; set; }


        public SceneBudget()
        {
            OverridenUnloadingSqrDistance = 17 * 17;
        }
        
        public void AddLoadingScene(string loadingScene)
        {
            loadingScenes.Add(loadingScene);
        }

        public void AddLoadedScene(string loadedScene)
        {
            loadingScenes.Remove(loadedScene);
            loadedScenes.Add(loadedScene);
        }
        
        public void CompleteUnloadedScene(string unloadedScene)
        {
            unloadingScenes.Remove(unloadedScene);
        }

        public void AddUnloadingScene(string unloadingScene)
        {
            unloadingScenes.Add(unloadingScene);
            loadedScenes.Remove(unloadingScene);
        }
        
        public bool TrySpendBudget()
        {
            return (loadingScenes.Count + loadedScenes.Count) < 4;
        }

        public bool WaitingForUnload()
        {
            return unloadingScenes.Count > 0;
        }
    }
}