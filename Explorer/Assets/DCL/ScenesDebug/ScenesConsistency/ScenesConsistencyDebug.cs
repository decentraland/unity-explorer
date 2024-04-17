using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ScenesDebug.ScenesConsistency.ChatTeleports;
using DCL.ScenesDebug.ScenesConsistency.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DCL.ScenesDebug.ScenesConsistency
{
    public class ScenesConsistencyDebug : MonoBehaviour
    {
        [SerializeField] private TextAsset scenesCoordinatesRaw;
        [SerializeField] private float delayBetweenTeleports = 5;
        [SerializeField] private string productionScene = "Main";

        //[SerializeField] private
        [Header("Debug")]
        [SerializeField] private List<SceneEntity> entities;

        private readonly IChatTeleport chatTeleport = new LogChatTeleport(
            new ChatTeleport(),
            ReportHub.WithReport(ReportCategory.SCENE_LOADING).Log
        );

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

            await chatTeleport.WaitReadyAsync();

            foreach (SceneEntity entity in entities)
            {
                chatTeleport.GoTo(entity.coordinate);
                await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenTeleports));
            }
        }
    }
}
