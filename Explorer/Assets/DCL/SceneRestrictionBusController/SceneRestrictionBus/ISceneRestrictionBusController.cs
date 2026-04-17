using System;

namespace DCL.SceneRestrictionBusController.SceneRestrictionBus
{
    public interface ISceneRestrictionBusController : IDisposable
    {
        void PushSceneRestriction(SceneRestriction.SceneRestriction sceneRestriction);
        void SubscribeToSceneRestriction(SceneRestrictionBusController.SceneRestrictionReceivedDelegate callback, bool replayActive = false);
        void UnsubscribeToSceneRestriction(SceneRestrictionBusController.SceneRestrictionReceivedDelegate callback);
    }
}
