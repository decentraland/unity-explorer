namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public class PassportBlockedRestriction : SceneRestrictionBase
    {
        public PassportBlockedRestriction(int entityId) : base()
        {
            Type = SceneRestrictions.PASSPORT_CANNOT_BE_OPENED;
            EntityId = entityId;
        }
    }
}
