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

        private IChatTeleport chatTeleport = null!;

        private void Start()
        {
            chatTeleport = new LogChatTeleport(
                new ChatTeleport(),
                Log
            );

            LaunchAsync().Forget();
        }

        private void Log(string message)
        {
            ReportHub.WithReport(ReportCategory.SCENE_LOADING).Log($"Debugging: {message}");
        }

        private async UniTaskVoid LaunchAsync()
        {
            Log("Scene loading...!");
            await SceneManager.LoadSceneAsync(productionScene, LoadSceneMode.Additive)!.ToUniTask();
            Log("Scene loaded!");

            entities = SceneEntities.FromText(scenesCoordinatesRaw)
                                    .Where(x => x.IsRunning() == false)
                                    .ToList();
            Log("Entities parsed!");

            await chatTeleport.WaitReadyAsync();

            foreach (SceneEntity entity in entities)
            {
                Log($"Executing entity: {entity.coordinate} {entity.status}");
                chatTeleport.GoTo(entity.coordinate);
                await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenTeleports));
            }
        }
    }
}
