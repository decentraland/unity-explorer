namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public class CameraLockedRestriction : SceneRestrictionBase
    {
        public CameraLockedRestriction(int entityId) : base()
        {
            Type = SceneRestrictions.CAMERA_LOCKED;
            EntityId = entityId;
        }
    }
}
