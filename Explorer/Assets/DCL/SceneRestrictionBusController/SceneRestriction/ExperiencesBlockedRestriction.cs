namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public class ExperiencesBlockedRestriction : SceneRestrictionBase
    {
        public ExperiencesBlockedRestriction(int entityId) : base()
        {
            Type = SceneRestrictions.EXPERIENCES_BLOCKED;
            EntityId = entityId;
        }
    }
}
