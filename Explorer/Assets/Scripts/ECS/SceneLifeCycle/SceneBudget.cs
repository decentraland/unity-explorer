using System.Collections.Generic;
using Arch.Core.Utils;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;

namespace ECS.SceneLifeCycle
{
    public class SceneBudget : IPerformanceBudget
    {
        private readonly Dictionary<string, PartitionComponent> loadingScenes = new ();
        private readonly HashSet<string> loadedScenes = new HashSet<string>();
        private readonly HashSet<string> unloadingScenes = new HashSet<string>();
        public double OverridenUnloadingSqrDistance { get; set; }


        public SceneBudget()
        {
            OverridenUnloadingSqrDistance = 17 * 17;
        }

        public void AddLoadingScene(string loadingScene, PartitionComponent partitionComponent)
        {
            loadingScenes.Add(loadingScene, partitionComponent);
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
            return loadingScenes.Count < 100;
        }

        public bool WaitingForUnload()
        {
            return unloadingScenes.Count > 0;
        }

        public bool CanUnload(string hash, PartitionComponent partitionComponentToCompare)
        {
            foreach (var keyValuePair in loadingScenes)
            {
                if (partitionComponentToCompare.RawSqrDistance < keyValuePair.Value.RawSqrDistance)
                {
                    UnityEngine.Debug.Log($"{hash} {partitionComponentToCompare.RawSqrDistance} is closer than {keyValuePair.Key} {keyValuePair.Value.RawSqrDistance}");

                    return true;
                }
            }

            return false;
        }
    }
}