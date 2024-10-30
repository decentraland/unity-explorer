namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public class PassportBlockedRestriction : SceneRestrictionBase
    {
        public PassportBlockedRestriction() : base()
        {
            Type = SceneRestrictions.PASSPORT_CANNOT_BE_OPENED;
        }
    }
}
