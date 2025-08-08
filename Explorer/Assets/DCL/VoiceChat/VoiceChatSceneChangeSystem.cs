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
        private readonly ISceneData sceneData;

        public VoiceChatSceneChangeSystem(
            World world,
            CommunityVoiceChatCallStatusService communityVoiceChatService,
            ISceneData sceneData) : base(world)
        {
            this.communityVoiceChatService = communityVoiceChatService;
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            // This system only responds to scene change events, no per-frame updates needed
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

