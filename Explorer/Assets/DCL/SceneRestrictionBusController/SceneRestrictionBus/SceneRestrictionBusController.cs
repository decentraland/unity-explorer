
namespace DCL.SceneRestrictionBusController.SceneRestrictionBus
{
    public class SceneRestrictionBusController : ISceneRestrictionBusController
    {
        public delegate void SceneRestrictionReceivedDelegate(SceneRestriction.SceneRestriction sceneRestriction);

        private SceneRestrictionReceivedDelegate sceneRestrictionReceivedDelegate;

        public void PushSceneRestriction(SceneRestriction.SceneRestriction sceneRestriction) =>
            sceneRestrictionReceivedDelegate?.Invoke(sceneRestriction);

        public void SubscribeToSceneRestriction(SceneRestrictionReceivedDelegate callback) =>
            sceneRestrictionReceivedDelegate += callback;
    }
}
