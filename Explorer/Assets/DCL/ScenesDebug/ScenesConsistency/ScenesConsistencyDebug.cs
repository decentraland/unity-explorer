using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ScenesDebug.ScenesConsistency.ChatTeleports;
using DCL.ScenesDebug.ScenesConsistency.Entities;
using DCL.ScenesDebug.ScenesConsistency.ReportLogs;
using System;
using System.Collections.Generic;
using System.IO;
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
        [SerializeField] private string reportPath = "Assets/Scenes/Debug/ScenesConsistency/Report.txt";

        //[SerializeField] private
        [Header("Debug")]
        [SerializeField] private List<SceneEntity> entities;

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private static void Log(string message)
        {
            ReportHub.WithReport(ReportCategory.SCENE_LOADING).Log($"Debugging: {message}");
        }

        private async UniTaskVoid LaunchAsync()
        {
            entities = SceneEntities.FromText(scenesCoordinatesRaw)
                                    .Where(x => x.IsRunning() == false)
                                    .ToList();

            var chatTeleport = new LogChatTeleport(
                new ChatTeleport(),
                Log
            );

            using var reportLog = new ReportLog(
                entities,
                new StreamWriter(
                    new FileStream(reportPath, FileMode.Create, FileAccess.Write),
                    System.Text.Encoding.UTF8,
                    1024,
                    false
                )
            );

            reportLog.Start();

            Log("Scene loading...!");
            await SceneManager.LoadSceneAsync(productionScene, LoadSceneMode.Additive)!.ToUniTask();
            Log("Scene loaded!");

            await chatTeleport.WaitReadyAsync();

            foreach (SceneEntity entity in entities)
            {
                Log($"Executing entity: {entity}");
                chatTeleport.GoTo(entity.coordinate);
                await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenTeleports));
            }

            Application.Quit();
        }
    }
}
