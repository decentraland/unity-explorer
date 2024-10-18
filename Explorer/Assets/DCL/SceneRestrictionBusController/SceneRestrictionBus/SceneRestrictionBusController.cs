using DCL.SceneRestrictionBusController.SceneRestriction;

namespace DCL.SceneRestrictionBusController.SceneRestrictionBus
{
    public class SceneRestrictionBusController : ISceneRestrictionBusController
    {
        public delegate void SceneRestrictionReceivedDelegate(ISceneRestriction sceneRestriction);

        private SceneRestrictionReceivedDelegate sceneRestrictionReceivedDelegate;

        public void PushSceneRestriction(ISceneRestriction sceneRestriction) =>
            sceneRestrictionReceivedDelegate?.Invoke(sceneRestriction);

        public void SubscribeToSceneRestriction(SceneRestrictionReceivedDelegate callback) =>
            sceneRestrictionReceivedDelegate += callback;
    }
}
