using DCL.SceneRestrictionBusController.SceneRestriction;

namespace DCL.SceneRestrictionBusController.SceneRestrictionBus
{
    public interface ISceneRestrictionBusController
    {
        void PushSceneRestriction(ISceneRestriction sceneRestriction);
        void SubscribeToSceneRestriction(SceneRestrictionBusController.SceneRestrictionReceivedDelegate callback);
    }
}
