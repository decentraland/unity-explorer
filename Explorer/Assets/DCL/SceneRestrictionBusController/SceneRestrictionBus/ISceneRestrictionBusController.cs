using System;

namespace DCL.SceneRestrictionBusController.SceneRestrictionBus
{
    public interface ISceneRestrictionBusController : IDisposable
    {
        void PushSceneRestriction(SceneRestriction.SceneRestriction sceneRestriction);
        void SubscribeToSceneRestriction(SceneRestrictionBusController.SceneRestrictionReceivedDelegate callback);
        void UnsubscribeToSceneRestriction(SceneRestrictionBusController.SceneRestrictionReceivedDelegate callback);
    }
}
