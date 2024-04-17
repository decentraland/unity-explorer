using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.ScenesDebug.ScenesConsistency.Entities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using SceneRunner;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DCL.ScenesDebug.ScenesConsistency
{
    public class ScenesConsistencyDebug : MonoBehaviour
    {
        [SerializeField] private TextAsset scenesCoordinatesRaw;
        [SerializeField] private string productionScene = "Main";


        //[SerializeField] private
        [Header("Debug")]
        [SerializeField] private List<SceneEntity> entities;

        private static readonly URLDomain ASSET_BUNDLE_URL = URLDomain.FromString("https://ab-cdn.decentraland.org/");

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private async UniTaskVoid LaunchAsync()
        {
            await SceneManager.LoadSceneAsync(productionScene, LoadSceneMode.Additive)!.ToUniTask();

            entities = SceneEntities.FromText(scenesCoordinatesRaw)
                                    .Where(x => x.IsRunning() == false)
                                    .ToList();

            foreach (SceneEntity entity in entities)
            {
                // await result.StartUpdateLoopAsync(60, destroyCancellationToken);
            }
        }
    }
}
