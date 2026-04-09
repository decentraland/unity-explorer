using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;

namespace DCL.VoiceChat
{
    public class ProximityVoiceSceneRestrictionWatcher : IDisposable
    {
        private readonly ISceneRestrictionBusController restrictionBus;
        private readonly ProximityVoiceChatStateModel stateModel;
        private readonly ReactivePropertyExtensions.DisposableSubscription<ISceneFacade?> subscription;

        private bool currentSceneBlocksVoice;

        public ProximityVoiceSceneRestrictionWatcher(
            IScenesCache scenesCache,
            ISceneRestrictionBusController restrictionBus,
            ProximityVoiceChatStateModel stateModel)
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
                RemoveRestriction();
        }

        private void OnCurrentSceneChanged(ISceneFacade? scene)
        {
            bool blocksVoice = scene != null &&
                               !scene.SceneData.SceneEntityDefinition.metadata.featureToggles.ProximityVoiceChatEnabled;
            if (blocksVoice == currentSceneBlocksVoice)
                return;

            if (blocksVoice)
                ApplyRestriction();
            else
                RemoveRestriction();
        }

        private void ApplyRestriction()
        {
            currentSceneBlocksVoice = true;
            restrictionBus.PushSceneRestriction(SceneRestriction.CreateProximityVoiceChatBlocked(SceneRestrictionsAction.APPLIED));
            stateModel.Suppress(ProximityVoiceChatStateModel.SUPPRESSION_SCENE);
        }

        private void RemoveRestriction()
        {
            currentSceneBlocksVoice = false;
            restrictionBus.PushSceneRestriction(SceneRestriction.CreateProximityVoiceChatBlocked(SceneRestrictionsAction.REMOVED));
            stateModel.Resume(ProximityVoiceChatStateModel.SUPPRESSION_SCENE);
        }
    }
}
