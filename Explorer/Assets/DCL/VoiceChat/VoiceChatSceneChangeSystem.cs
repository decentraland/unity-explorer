using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.VoiceChat;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.VoiceChat.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.VOICE_CHAT)]
    public partial class VoiceChatSceneChangeSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly CommunityVoiceChatCallStatusService communityVoiceChatService;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private ISceneFacade? currentScene;

        public VoiceChatSceneChangeSystem(
            World world,
            CommunityVoiceChatCallStatusService communityVoiceChatService,
            ICurrentSceneInfo currentSceneInfo) : base(world)
        {
            this.communityVoiceChatService = communityVoiceChatService;
            this.currentSceneInfo = currentSceneInfo;
        }

        protected override void Update(float t)
        {
            // Check if the current scene has changed by monitoring the currentSceneInfo
            // This system will be notified via OnSceneIsCurrentChanged when scenes become current/not current
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            if (isCurrent)
            {
                OnSceneBecameCurrent();
            }
            else
            {
                OnSceneLeft();
            }
        }

        private void OnSceneBecameCurrent()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"VOICE_CHAT_SCENE_SYSTEM: Scene became current - {sceneData.SceneEntityDefinition?.id ?? "Unknown"}");

            // Check if this scene has an active community voice chat
            communityVoiceChatService.OnSceneBecameCurrent(sceneData);
        }

        private void OnSceneLeft()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"VOICE_CHAT_SCENE_SYSTEM: Scene left - {sceneData.SceneEntityDefinition?.id ?? "Unknown"}");

            // Notify that we left the scene
            communityVoiceChatService.OnSceneLeft();
        }
    }
}

