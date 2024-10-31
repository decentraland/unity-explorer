using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using ECS.Abstract;
using ECS.SceneLifeCycle.Reporting;
using SceneRunner.EmptyScene;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class CheckFailedScenesStateSystem : BaseUnityLoopSystem
    {
        private readonly ISceneReadinessReportQueue sceneReadinesReportQueue;
        private readonly IChatHistory chatHistory;
        private PooledLoadReportList? reports;

        public CheckFailedScenesStateSystem(World world, ISceneReadinessReportQueue sceneReadinesReportQueue, IChatHistory chatHistory) : base(world)
        {
            this.sceneReadinesReportQueue = sceneReadinesReportQueue;
            this.chatHistory = chatHistory;
        }

        protected override void Update(float t)
        {
            CheckFailedSceneQuery(World);
        }

        [Query]
        private void CheckFailedScene(ref ISceneFacade sceneFacade)
        {
            if (sceneFacade.IsBrokenScene)
            {
                if (sceneReadinesReportQueue.TryDequeue(sceneFacade.SceneData.Parcels, out reports))
                {
                    for (int i = 0; i < reports!.Value.Count; i++)
                    {
                        var report = reports.Value[i];
                        report.SetProgress(1);
                    }

                    reports.Value.Dispose();
                    reports = null;
                    chatHistory.AddMessage(ChatMessage.NewFromSystem($"🔴 Scene {sceneFacade.SceneData.SceneEntityDefinition.metadata.scene.DecodedBase} failed to load"));
                }
            }
        }
    }
}