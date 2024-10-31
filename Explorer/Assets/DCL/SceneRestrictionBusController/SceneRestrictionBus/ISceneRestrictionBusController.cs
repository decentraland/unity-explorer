
namespace DCL.SceneRestrictionBusController.SceneRestrictionBus
{
    public interface ISceneRestrictionBusController
    {
        void PushSceneRestriction(SceneRestriction.SceneRestriction sceneRestriction);
        void SubscribeToSceneRestriction(SceneRestrictionBusController.SceneRestrictionReceivedDelegate callback);
    }
}
