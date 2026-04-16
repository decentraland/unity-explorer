using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;

namespace DCL.VoiceChat
{
    public class NearbyVoiceSceneRestrictionWatcher : IDisposable
    {
        private readonly ISceneRestrictionBusController restrictionBus;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly ReactivePropertyExtensions.DisposableSubscription<ISceneFacade?> subscription;

        private bool currentSceneBlocksVoice;

        public NearbyVoiceSceneRestrictionWatcher(IScenesCache scenesCache, ISceneRestrictionBusController restrictionBus, NearbyVoiceChatStateModel stateModel)
        {
            this.restrictionBus = restrictionBus;
            this.stateModel = stateModel;

            OnCurrentSceneChanged(scenesCache.CurrentScene.Value);
            subscription = scenesCache.CurrentScene.Subscribe(OnCurrentSceneChanged);
        }

        public void Dispose()
        {
            subscription.Dispose();

            if (currentSceneBlocksVoice)
            {
                currentSceneBlocksVoice = false;
                restrictionBus.PushSceneRestriction(SceneRestriction.CreateNearbyVoiceChatBlocked(SceneRestrictionsAction.REMOVED));
                stateModel.Resume(NearbyVoiceChatStateModel.SUPPRESSION_SCENE);
            }
        }

        private void OnCurrentSceneChanged(ISceneFacade? scene)
        {
            bool blocksVoice = scene != null && !scene.SceneData.SceneEntityDefinition.metadata.featureToggles.NearbyVoiceChatEnabled;

            if (blocksVoice == currentSceneBlocksVoice)
                return;

            currentSceneBlocksVoice = blocksVoice;

            if (blocksVoice)
            {
                restrictionBus.PushSceneRestriction(SceneRestriction.CreateNearbyVoiceChatBlocked(SceneRestrictionsAction.APPLIED));
                stateModel.Suppress(NearbyVoiceChatStateModel.SUPPRESSION_SCENE);
            }
            else
            {
                restrictionBus.PushSceneRestriction(SceneRestriction.CreateNearbyVoiceChatBlocked(SceneRestrictionsAction.REMOVED));
                stateModel.Resume(NearbyVoiceChatStateModel.SUPPRESSION_SCENE);
            }
        }
    }
}
