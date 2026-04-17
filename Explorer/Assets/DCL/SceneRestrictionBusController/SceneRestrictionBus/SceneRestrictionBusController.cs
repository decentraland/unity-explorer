using System.Collections.Generic;

namespace DCL.SceneRestrictionBusController.SceneRestrictionBus
{
    public class SceneRestrictionBusController : ISceneRestrictionBusController
    {
        public delegate void SceneRestrictionReceivedDelegate(SceneRestriction.SceneRestriction sceneRestriction);

        private readonly Dictionary<SceneRestriction.SceneRestrictions, SceneRestriction.SceneRestriction> activeRestrictions = new ();
        private SceneRestrictionReceivedDelegate sceneRestrictionReceivedDelegate;

        public void PushSceneRestriction(SceneRestriction.SceneRestriction sceneRestriction)
        {
            if (sceneRestriction.Action == SceneRestriction.SceneRestrictionsAction.APPLIED)
                activeRestrictions[sceneRestriction.Type] = sceneRestriction;
            else
                activeRestrictions.Remove(sceneRestriction.Type);

            sceneRestrictionReceivedDelegate?.Invoke(sceneRestriction);
        }

        public void SubscribeToSceneRestriction(SceneRestrictionReceivedDelegate callback)
        {
            sceneRestrictionReceivedDelegate += callback;

            foreach (var restriction in activeRestrictions.Values)
                callback(restriction);
        }

        public void UnsubscribeToSceneRestriction(SceneRestrictionReceivedDelegate callback) =>
            sceneRestrictionReceivedDelegate -= callback;

        public void Dispose()
        {
            sceneRestrictionReceivedDelegate = null;
            activeRestrictions.Clear();
        }
    }
}
