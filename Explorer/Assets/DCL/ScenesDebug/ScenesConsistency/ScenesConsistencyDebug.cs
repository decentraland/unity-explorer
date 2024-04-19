using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.ScenesDebug.ScenesConsistency.ChatTeleports;
using DCL.ScenesDebug.ScenesConsistency.Conditions;
using DCL.ScenesDebug.ScenesConsistency.DelayedResources;
using DCL.ScenesDebug.ScenesConsistency.Entities;
using DCL.ScenesDebug.ScenesConsistency.ReportLogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DCL.ScenesDebug.ScenesConsistency
{
    public class ScenesConsistencyDebug : MonoBehaviour
    {
        [SerializeField] private NextSceneDelayType nextSceneDelayType = NextSceneDelayType.ByTime;
        [SerializeField] private float delayBetweenTeleports = 5;
        [Space]
        [SerializeField] private TextAsset scenesCoordinatesRaw;
        [SerializeField] private string productionScene = "Main";
        [SerializeField] private string reportDirectory = "Assets/Scenes/Debug/ScenesConsistency/Reports";

        //[SerializeField] private
        [Header("Debug")]
        [SerializeField] private List<SceneEntity> entities;

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private static void Log(string message)
        {
            ReportHub.WithReport(ReportCategory.UNSPECIFIED).Log($"Debugging: {message}");
        }

        private void Stop()
        {
            EditorApplication.isPlaying = false;
        }

        private async UniTaskVoid LaunchAsync()
        {
            entities = SceneEntities.FromText(scenesCoordinatesRaw)
                                    .Where(x => x.IsRunning() == false)
                                    .ToList();

            var chatView = new DelayedResource<ChatView>(FindObjectOfType<ChatView>);

            var chatTeleport = new LogChatTeleport(
                new ChatTeleport(chatView),
                Log
            );

            INextSceneDelay nextSceneDelay =
                nextSceneDelayType switch
                {
                    NextSceneDelayType.ByTime => new ByTimeNextSceneDelay(TimeSpan.FromSeconds(delayBetweenTeleports)),
                    NextSceneDelayType.BySubmit => new BySubmitNextSceneDelay(chatView),
                    _ => throw new ArgumentOutOfRangeException()
                };

            Log("Scene loading...!");
            await SceneManager.LoadSceneAsync(productionScene, LoadSceneMode.Additive)!.ToUniTask();
            Log("Scene loaded!");

            await chatTeleport.WaitReadyAsync();

            var current = 0;

            await nextSceneDelay.WaitAsync();

            foreach (SceneEntity entity in entities)
            {
                using var reportLog = new ReportLog(
                    entities,
                    new StreamWriter(
                        new FileStream(
                            Path.Combine(
                                reportDirectory,
                                $"{entity.coordinate.x}_{entity.coordinate.y}.txt"
                            ),
                            FileMode.Create,
                            FileAccess.Write
                        ),
                        System.Text.Encoding.UTF8,
                        1024,
                        false
                    )
                    {
                        AutoFlush = true,
                    }
                );

                reportLog.Start();

                Log($"Executing {++current} / {entities.Count} entity: {entity}");
                chatTeleport.GoTo(entity.coordinate);
                await nextSceneDelay.WaitAsync();
            }

            Log("Ready to quit!");
            Stop();
        }
    }
}
