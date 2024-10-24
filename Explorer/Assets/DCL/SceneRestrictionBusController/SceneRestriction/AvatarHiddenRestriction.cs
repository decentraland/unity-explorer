namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public class AvatarHiddenRestriction : SceneRestrictionBase
    {
        public AvatarHiddenRestriction(int entityId) : base()
        {
            Type = SceneRestrictions.AVATAR_HIDDEN;
            EntityId = entityId;
        }
    }
}
